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
    }
}
