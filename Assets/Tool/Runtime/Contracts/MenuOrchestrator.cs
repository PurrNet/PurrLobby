using UnityEngine;

namespace PurrNet.Lobby
{
    [CreateAssetMenu(menuName = "PurrNet/Lobby/Menu Orchestrator")]
    public class MenuOrchestrator : ScriptableObject
    {
        public SessionProvider sessionProvider;
        public LobbyProvider lobbyProvider;
        public MatchmakingProvider matchmakingProvider;
        public GameStarterProvider gameStarterProvider;
    }
}
