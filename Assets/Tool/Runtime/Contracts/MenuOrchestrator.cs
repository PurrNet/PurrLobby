using UnityEngine;

namespace PurrLobby
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
