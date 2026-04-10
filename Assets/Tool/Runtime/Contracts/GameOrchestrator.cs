using UnityEngine;

namespace PurrNet.Lobby
{
    [CreateAssetMenu(menuName = "PurrNet/Lobby/Menu Orchestrator")]
    public class GameOrchestrator : ScriptableObject
    {
        public SessionProvider sessionProvider;
        public LobbyProvider lobbyProvider;
        public MatchmakingProvider matchmakingProvider;
        public GameAllocatorProvider gameAllocator;
    }
}
