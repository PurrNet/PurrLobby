using UnityEngine;

namespace PurrNet.Lobby
{
    [CreateAssetMenu(menuName = "PurrLobby/Menu Orchestrator")]
    public class GameOrchestrator : ScriptableObject
    {
        public SessionProvider sessionProvider;
        public LobbyProvider lobbyProvider;
        public MatchmakingProvider matchmakingProvider;
        public GameAllocatorProvider gameAllocator;

        /// <summary>
        /// The lobby the player is currently in, if any. Held here so the game
        /// scene can leave it on the way back to the menu. Survives scene loads
        /// because this asset is shared between the menu and game scenes.
        /// </summary>
        [System.NonSerialized] public ILobby activeLobby;

        /// <summary>Why the player last left the game scene. The menu reads this to react.</summary>
        [System.NonSerialized] public GameExitReason lastExitReason;

        /// <summary>Scene active when the game was launched; the game scene returns here. Set by the allocator at load.</summary>
        [System.NonSerialized] public string menuScene;

        /// <summary>The orchestrator currently driving the process, set by LobbyManager on menu boot.</summary>
        public static GameOrchestrator active { get; internal set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            active = null;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        }

#if UNITY_EDITOR
        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change != UnityEditor.PlayModeStateChange.ExitingPlayMode)
                return;

            if (active)
            {
                active.activeLobby = null;
                active.lastExitReason = GameExitReason.None;
                active.menuScene = null;
            }

            active = null;
        }
#endif
    }
}
