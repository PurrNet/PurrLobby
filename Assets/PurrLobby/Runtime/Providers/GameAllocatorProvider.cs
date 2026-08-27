using System;
using System.Threading.Tasks;
using PurrNet.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurrNet.Lobby
{
    public struct GameStartResponse
    {
        public bool success;
        public string error;
        public ConnectionInfo connection;

        public static GameStartResponse Success(ConnectionInfo connection) =>
            new GameStartResponse { success = true, connection = connection };
        public static GameStartResponse Failure(string error) =>
            new GameStartResponse { success = false, error = error };

        public override string ToString()
        {
            return $"success: {success}, error: {error}, connection: {{ {connection} }}";
        }
    }

    public abstract class GameAllocatorProvider : ScriptableObject
    {
        /// <summary>Called once after the session provider has logged in.</summary>
        public virtual Task Initialize() => Task.CompletedTask;

        /// <summary>Provider-local cleanup.</summary>
        public virtual Task Logout() => Task.CompletedTask;

        public abstract Task<GameStartResponse> AllocateGame(ILobby lobby);

        public abstract Task LoadGame(ILobby lobby);

        /// <summary>Allocates a game from a matchmaking result. If the result has a lobby, delegates to the lobby overload; otherwise returns the pre-populated connection info.</summary>
        public virtual Task<GameStartResponse> AllocateGame(MatchResult matchResult)
        {
            if (matchResult.lobby != null)
                return AllocateGame(matchResult.lobby);
            return Task.FromResult(GameStartResponse.Success(matchResult.connection));
        }

        /// <summary>Loads the game scene from a matchmaking result. Delegates to the lobby overload by default.</summary>
        public virtual Task LoadGame(MatchResult matchResult)
        {
            return LoadGame(matchResult.lobby);
        }

        /// <summary>
        /// False for allocators that connect to a dedicated server (e.g. Edgegap):
        /// <see cref="Connect"/> then always starts a client, never a host.
        /// </summary>
        protected virtual bool supportsHosting => true;

        /// <summary>
        /// Configures the transport via <see cref="ConfigureTransport"/> and starts
        /// the host or client on <see cref="NetworkManager.main"/>.
        /// </summary>
        public void Connect(ConnectionInfo connection, bool shouldBeHost)
        {
            if (GameSession.TryGet(SceneManager.GetActiveScene(), out var session) && session.isExiting)
            {
                PurrLogger.LogWarning("Skipping the game connection because its `GameSession` is already exiting.");
                return;
            }

            var networkManager = NetworkManager.main;
            if (!networkManager)
            {
                PurrLogger.LogError("No `NetworkManager` found in the scene.");
                return;
            }

            if (networkManager.shouldAutoStartServer || networkManager.shouldAutoStartClient)
            {
                PurrLogger.LogError("`NetworkManager` is set to auto start (has auto start flags). Please disable auto start and try again.");
                return;
            }

            if (shouldBeHost && !supportsHosting)
            {
                PurrLogger.LogWarning($"`{name}` does not support hosting; connecting as client instead.");
                shouldBeHost = false;
            }

            if (!ConfigureTransport(networkManager, connection, shouldBeHost))
                return;

            if (shouldBeHost)
                 networkManager.StartHost();
            else networkManager.StartClient();
        }

        /// <summary>
        /// Configure (and if needed add and assign) the transport on the manager.
        /// Return false to abort the connection attempt (log the reason).
        /// </summary>
        protected abstract bool ConfigureTransport(NetworkManager manager, ConnectionInfo connection, bool asHost);

        /// <summary>
        /// Loads the game scene and captures the menu scene to return to. Concrete allocators call this
        /// from their <see cref="LoadGame(ILobby)"/> instead of loading the scene themselves.
        /// </summary>
        protected Task LoadGameScene(string gameScene)
        {
            if (string.IsNullOrEmpty(gameScene))
                throw new Exception($"Game scene is not set. Please set the game scene in the inspector of `{name}`.");

            var orch = GameOrchestrator.active;
            if (orch && string.IsNullOrEmpty(orch.menuScene))
                orch.menuScene = SceneManager.GetActiveScene().name;

            // The lobby flow drives the network here, but the game scene may carry
            // auto-start flags for the play-the-scene-directly workflow. Suppress
            // them just for this load; StartFlagsScope re-enables once the scene's
            // Start window has passed.
            NetworkManager.DisableFlags();

            var asyncOp = SceneManager.LoadSceneAsync(gameScene);
            if (asyncOp == null)
            {
                NetworkManager.EnableFlags();
                PurrLogger.LogError($"Loading scene `{gameScene}` failed.");
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>();
            asyncOp.completed += _ =>
            {
                GameSession.EnsureInScene(SceneManager.GetActiveScene());
                StartFlagsScope.Release();
                tcs.SetResult(true);
            };
            return tcs.Task;
        }

        /// <summary>
        /// Closes a <see cref="NetworkManager.DisableFlags"/> scope once the freshly
        /// loaded scene's Start callbacks (where auto-start runs) are behind us.
        /// </summary>
        private class StartFlagsScope : MonoBehaviour
        {
            private bool _released;

            public static void Release()
            {
                // Scene-owned (not DontSave) so if the scene goes away early,
                // OnDestroy still balances the counter.
                var go = new GameObject("PurrLobby StartFlags Scope")
                {
                    hideFlags = HideFlags.HideInHierarchy
                };
                go.AddComponent<StartFlagsScope>();
            }

            private System.Collections.IEnumerator Start()
            {
                // Two frames: activation ran Awake/OnEnable, the next cycle runs
                // Start (incl. NetworkManager.AutoStart), then we lift the scope.
                yield return null;
                yield return null;
                ReleaseNow();
                Destroy(gameObject);
            }

            private void OnDestroy()
            {
                // If the scene dies before the coroutine finishes, still balance
                // the counter — a stuck scope would disable auto-start globally.
                ReleaseNow();
            }

            private void ReleaseNow()
            {
                if (_released)
                    return;
                _released = true;
                NetworkManager.EnableFlags();
            }
        }

        /// <summary>Gets or adds a component on the given GameObject.</summary>
        protected static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent<T>(out var existing) ? existing : go.AddComponent<T>();
        }
    }
}
