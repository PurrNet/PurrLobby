using UnityEngine;

namespace PurrLobby
{
    [CreateAssetMenu(menuName = "PurrNet/Lobby/Menu Orchestrator")]
    public class MenuOrchestrator : ScriptableObject
    {
        public LobbyProvider lobbyProvider;
        public MatchmakingProvider matchmakingProvider;
        public GameStarterProvider gameStarterProvider;

        public void Initialize()
        {
            lobbyProvider?.Initialize(this);
            matchmakingProvider?.Initialize(this);
            gameStarterProvider?.Initialize(this);
        }
    }
}
