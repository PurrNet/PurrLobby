using JetBrains.Annotations;

namespace PurrNet.Lobby
{
    [UsedImplicitly]
    public static class ILobbyExtensions
    {
        public static bool TryGetPlayer(this ILobby lobby, string playerId, out IPlayer player)
        {
            var players = lobby.players;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].id == playerId)
                {
                    player = players[i];
                    return true;
                }
            }

            player = null;
            return false;
        }
    }
}
