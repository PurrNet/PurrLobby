using PurrNet.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_6000_3_OR_NEWER
using SceneKey = UnityEngine.SceneManagement.SceneHandle;
#else
using SceneKey = System.Int32;
#endif

namespace PurrNet.Lobby
{
    /// <summary>
    /// A scene network object in the game scene. Lets the server end the game
    /// for every player via <see cref="EndGame"/>.
    /// </summary>
    public class GameOverBroadcaster : NetworkIdentity
    {
        private static readonly Dictionary<SceneKey, GameOverBroadcaster> _byScene = new();

        private GameSession _resolved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _byScene.Clear();

        private void Awake()
        {
            _byScene[GetSceneKey(gameObject.scene)] = this;
        }

        protected override void OnDestroy()
        {
            var sceneKey = GetSceneKey(gameObject.scene);
            if (_byScene.TryGetValue(sceneKey, out var broadcaster) && broadcaster == this)
                _byScene.Remove(sceneKey);

            base.OnDestroy();
        }

        internal static bool TryGet(Scene scene, out GameOverBroadcaster broadcaster)
        {
            return _byScene.TryGetValue(GetSceneKey(scene), out broadcaster) && broadcaster;
        }

        private static SceneKey GetSceneKey(Scene scene) => scene.handle;

        /// <summary>
        /// Server-only. Ends the game and returns every player to the menu.
        /// </summary>
        public void EndGame()
        {
            if (!isSpawned)
            {
                PurrLogger.LogError("`GameOverBroadcaster.EndGame` was called before the object spawned.", this);
                return;
            }

            if (!isServer)
            {
                PurrLogger.LogError("`GameOverBroadcaster.EndGame` must be called on the server.", this);
                return;
            }

            var session = ResolveSession();
            EndGameRpc();
            FlushReliableRpcs();

            if (session)
                session.GameEnded();
            else
                PurrLogger.LogError("`GameOverBroadcaster` could not find a `GameSession` to end the game.", this);
        }

        /// <summary>
        /// Server-only. Announces a listen host's voluntary departure before the
        /// host tears down the transport, so observers leave without reconnecting.
        /// </summary>
        internal bool NotifyHostLeaving()
        {
            if (!isSpawned || !isServer)
                return false;

            var session = ResolveSession();
            HostLeavingRpc();
            FlushReliableRpcs();

            if (session)
                session.HostLeft(true);
            else
                PurrLogger.LogError("`GameOverBroadcaster` could not find a `GameSession` for host departure.", this);

            return true;
        }

        [ObserversRpc(excludeSender: true)]
        private void EndGameRpc()
        {
            var session = ResolveSession();

            if (session)
                session.GameEnded();
            else
                PurrLogger.LogError("`GameOverBroadcaster` could not find a `GameSession` to end the game.", this);
        }

        [ObserversRpc(excludeSender: true)]
        private void HostLeavingRpc()
        {
            var session = ResolveSession();

            if (session)
                session.HostLeft(isServer);
            else
                PurrLogger.LogError("`GameOverBroadcaster` could not find a `GameSession` for host departure.", this);
        }

        /// <summary>
        /// Hands reliable RPCs to the active transport and flushes that transport
        /// before the local session is allowed to stop it.
        /// </summary>
        private void FlushReliableRpcs()
        {
            var manager = networkManager;
            if (!manager)
                return;

            try
            {
                manager.FlushBatchedRPCs();
                manager.rawTransport?.SendMessages(0f);
            }
            catch (Exception e)
            {
                // A transport failure must not strand the host in a session it
                // explicitly chose to end. The lobby departure remains a fallback
                // terminal signal for remote clients.
                PurrLogger.LogException(e, this);
            }
        }

        /// <summary>Resolves the GameSession in this broadcaster's own scene, so a server hosting several scenes ends the right one.</summary>
        private GameSession ResolveSession()
        {
            if (_resolved || GameSession.TryGet(gameObject.scene, out _resolved))
                return _resolved;

            return GameSession.instance;
        }
    }
}
