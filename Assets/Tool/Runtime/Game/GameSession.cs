using System;
using System.Collections;
using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Transports;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurrNet.Lobby
{
    /// <summary>
    /// Owns the return-to-menu flow for the game scene: voluntary leave,
    /// server-driven game over, and reconnect-then-give-up on an unexpected
    /// disconnect. A plain MonoBehaviour on purpose - it must stay alive while
    /// the network is down to drive the reconnect loop and the scene load.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        [Tooltip("Optional override. The orchestrator asset shared with the menu scene; auto-resolved from GameOrchestrator.Active when empty.")]
        [SerializeField] private GameOrchestrator _orchestrator;

        [Tooltip("Optional override. Scene to return to when leaving the game; auto-resolved from the launch scene when empty.")]
        [SerializeField, PurrScene] private string _menuScene;

        [Header("Reconnect (clients only)")]
        [Tooltip("How long to keep retrying after an unexpected disconnect before giving up and returning to the menu.")]
        [SerializeField] private float _reconnectTimeout = 15f;

        [Tooltip("Delay between reconnect attempts.")]
        [SerializeField] private float _reconnectInterval = 2f;

        /// <summary>The most recently registered <see cref="GameSession"/>. For the common single-session case; prefer <see cref="TryGet"/> when the scene is known.</summary>
        public static GameSession instance { get; private set; }

        private static readonly Dictionary<int, GameSession> _byScene = new();

        /// <summary>Gets the <see cref="GameSession"/> living in the given scene, if any.</summary>
        public static bool TryGet(Scene scene, out GameSession session)
        {
            if (_byScene.TryGetValue(scene.handle, out session) && session)
                return true;
            session = null;
            return false;
        }

        /// <summary>Returns the scene's <see cref="GameSession"/>, creating one if absent.</summary>
        public static GameSession EnsureInScene(Scene scene)
        {
            if (TryGet(scene, out var existing))
                return existing;

            var go = new GameObject(nameof(GameSession));
            if (go.scene != scene)
                SceneManager.MoveGameObjectToScene(go, scene);
            return go.AddComponent<GameSession>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _byScene.Clear();
            instance = null;
        }

        /// <summary>True while reconnect attempts are in progress after an unexpected disconnect.</summary>
        public bool isReconnecting { get; private set; }

        /// <summary>Raised when <see cref="isReconnecting"/> changes.</summary>
        public event Action<bool> onReconnectingChanged;

        /// <summary>Raised once when the session begins exiting, with the reason for the exit.</summary>
        public event Action<GameExitReason> onExiting;

        private NetworkManager _manager;
        private bool _exiting;
        private Coroutine _reconnectRoutine;

        private void Awake()
        {
            var scene = gameObject.scene;
            if (TryGet(scene, out var existing) && existing != this)
            {
                PurrLogger.LogError("Multiple `GameSession` components in the same scene; disabling the extra one.", this);
                enabled = false;
                return;
            }

            _byScene[scene.handle] = this;
            instance = this;
        }

        private void Start()
        {
            if (!_orchestrator)
                _orchestrator = GameOrchestrator.active;

            if (string.IsNullOrEmpty(_menuScene) && _orchestrator)
                _menuScene = _orchestrator.menuScene;

            if (!_orchestrator)
                PurrLogger.LogError("`GameSession` has no `GameOrchestrator` assigned - it cannot leave the lobby.", this);

            if (string.IsNullOrEmpty(_menuScene))
                PurrLogger.LogError("`GameSession` has no menu scene assigned - it cannot return to the menu.", this);

            _manager = NetworkManager.main;

            if (!_manager)
            {
                PurrLogger.LogError("`GameSession` found no `NetworkManager` in the scene.", this);
                return;
            }

            _manager.onClientConnectionState += OnClientConnectionState;
            _manager.onServerConnectionState += OnServerConnectionState;
        }

        private void OnDestroy()
        {
            if (_manager)
            {
                _manager.onClientConnectionState -= OnClientConnectionState;
                _manager.onServerConnectionState -= OnServerConnectionState;
            }

            if (_byScene.TryGetValue(gameObject.scene.handle, out var s) && s == this)
                _byScene.Remove(gameObject.scene.handle);

            if (instance == this)
                instance = null;
        }

        /// <summary>The player chose to leave. Returns to the menu immediately, with no reconnect.</summary>
        public void LeaveToMenu() => ExitToMenu(GameExitReason.LeftByChoice);

        /// <summary>Invoked on every client by <see cref="GameOverBroadcaster"/> when the server ends the game.</summary>
        public void GameEnded() => ExitToMenu(GameExitReason.GameOver);

        private void OnClientConnectionState(ConnectionState state)
        {
            if (_exiting || state != ConnectionState.Disconnected)
                return;

            if (_manager.isServer)
            {
                ExitToMenu(GameExitReason.ConnectionLost);
                return;
            }

            if (!isReconnecting)
            {
                SetReconnecting(true);
                _reconnectRoutine = StartCoroutine(ReconnectRoutine());
            }
        }

        private void OnServerConnectionState(ConnectionState state)
        {
            if (_exiting || state != ConnectionState.Disconnected)
                return;

            ExitToMenu(GameExitReason.ConnectionLost);
        }

        private IEnumerator ReconnectRoutine()
        {
            float interval = Mathf.Max(0.1f, _reconnectInterval);
            float elapsed = 0f;

            while (elapsed < _reconnectTimeout)
            {
                if (_exiting)
                    yield break;

                switch (_manager.clientState)
                {
                    case ConnectionState.Connected:
                        SetReconnecting(false);
                        _reconnectRoutine = null;
                        yield break;
                    case ConnectionState.Disconnected:
                        _manager.StartClient();
                        break;
                    case ConnectionState.Connecting:
                    case ConnectionState.Disconnecting:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                yield return new WaitForSecondsRealtime(interval);
                elapsed += interval;
            }

            SetReconnecting(false);
            _reconnectRoutine = null;
            ExitToMenu(GameExitReason.ConnectionLost);
        }

        private void SetReconnecting(bool value)
        {
            if (isReconnecting == value)
                return;

            isReconnecting = value;
            onReconnectingChanged?.Invoke(value);
        }

        private void ExitToMenu(GameExitReason reason)
        {
            if (_exiting)
                return;

            _exiting = true;

            if (_reconnectRoutine != null)
            {
                StopCoroutine(_reconnectRoutine);
                _reconnectRoutine = null;
            }

            SetReconnecting(false);

            if (_orchestrator)
                _orchestrator.lastExitReason = reason;

            try { onExiting?.Invoke(reason); }
            catch (Exception e) { Debug.LogException(e); }

            bool isDedicatedServer = _manager && _manager.isServerOnly;

            if (_manager)
            {
                if (_manager.isServer)
                    _manager.StopServer();
                if (_manager.isClient)
                    _manager.StopClient();
            }

            if (_orchestrator != null && _orchestrator.activeLobby != null)
            {
                _orchestrator.activeLobby.LeaveLobby();
                _orchestrator.activeLobby = null;
            }

            if (isDedicatedServer)
            {
                Debug.Log($"`GameSession` ended on a dedicated server ({reason}); skipping menu load.", this);
                return;
            }

            if (string.IsNullOrEmpty(_menuScene))
            {
                PurrLogger.LogError("`GameSession` has no menu scene set - cannot return to the menu.", this);
                return;
            }

            SceneManager.LoadSceneAsync(_menuScene);
        }
    }
}
