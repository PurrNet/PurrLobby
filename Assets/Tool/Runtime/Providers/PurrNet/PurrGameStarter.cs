using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PurrLobby.Internal;

namespace PurrLobby
{
    public sealed class PurrGameStarter : IGameStarter
    {
        readonly LobbyApiClient _api;

        internal PurrGameStarter(LobbyApiClient api)
        {
            _api = api;
        }

        public async Task<ConnectionInfo> StartGame(GameStartRequest request, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();

            string response = await _api.PostAsync($"/api/lobby/{request.lobbyId}/start");
            var data = Json.ParseObject(response);

            cancellation.ThrowIfCancellationRequested();

            var connection = new ConnectionInfo
            {
                serverAddress = data.GetString("serverAddress"),
                serverPort = data.GetInt("serverPort"),
                connectionToken = data.GetString("connectionToken"),
                metadata = data.GetStringDict("metadata")
            };

            return connection;
        }
    }
}
