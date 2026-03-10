using System.Collections.Generic;
using System.Threading.Tasks;
using PurrNet.Services;
using PurrNet.UI;
using UnityEngine;

namespace PurrLobby.PurrNet
{
    [CreateAssetMenu(menuName = "PurrLobby/PurrNet/Lobby Provider", order = -201)]
    public class PurrNetLobbyProvider : LobbyProvider
    {
        [SerializeField] private PurrNetSessionProvider _sessionProvider;
        [SerializeField] private int _maxPlayers = 4;

        public override int maxPlayer => _maxPlayers;

        public override async Task Login(ViewStack stack)
        {
            await _sessionProvider.Login(stack);
        }

        public override void Logout()
        {
            _sessionProvider.Logout();
        }

        public override async Task<LobbyResponse> CreateLobby(LobbySettings settings)
        {
            var services = PurrServices.instance;

            var result = await services.lobbies.CreateAsync(new CreateLobbyOptions
            {
                maxPlayers = settings.maxPlayers,
                visibility = settings.visibility == LobbyVisibility.Public ?
                    global::PurrNet.Services.LobbyVisibility.Public :
                    global::PurrNet.Services.LobbyVisibility.Private,
                name = settings.name,
                metadata = settings.metadata
            });

            var response = new LobbyResponse
            {
                error = result.error,
                success = result.success,
                lobby = result.success ? new PurrNetLobby(services.lobbies, result.lobby, result.playerToken) : null
            };

            return response;
        }

        public override async Task<LobbyResponse> JoinLobby(string lobbyId)
        {
            var services = PurrServices.instance;
            var result = await services.lobbies.JoinAsync(lobbyId);

            if (!result.success)
                return LobbyResponse.Failure(result.error);

            var snapshot = await services.lobbies.PollAsync(result.lobbyId);

            if (!snapshot.success)
                return LobbyResponse.Failure(snapshot.error);

            var lobby = new PurrNetLobby(services.lobbies, snapshot.snapshot.lobby, result.playerToken);
            return LobbyResponse.Success(lobby);
        }

        public override async Task<LobbyResponse> JoinLobbyByCode(string code)
        {
            var services = PurrServices.instance;
            var result = await services.lobbies.JoinByCodeAsync(code);

            if (!result.success)
                return LobbyResponse.Failure(result.error);

            var snapshot = await services.lobbies.PollAsync(result.lobbyId);

            if (!snapshot.success)
                return LobbyResponse.Failure(snapshot.error);

            var lobby = new PurrNetLobby(services.lobbies, snapshot.snapshot.lobby, result.playerToken);
            return LobbyResponse.Success(lobby);
        }

        public override async Task<LobbyResponse> JoinRandom(LobbyQuery query = default)
        {
            var services = PurrServices.instance;
            var result = await services.lobbies.QuickJoinAsync(query.dataFilters);

            if (!result.success)
                return LobbyResponse.Failure(result.error);

            var snapshot = await services.lobbies.PollAsync(result.lobbyId);

            if (!snapshot.success)
                return LobbyResponse.Failure(snapshot.error);

            var lobby = new PurrNetLobby(services.lobbies, snapshot.snapshot.lobby, result.playerToken);
            return LobbyResponse.Success(lobby);
        }

        public override async Task<LobbyCollectionResponse> QueryLobbies(LobbyQuery query = default)
        {
            var services = PurrServices.instance;
            var result = await services.lobbies.ListAsync();

            if (!result.success)
                return LobbyCollectionResponse.Failure(result.error);

            var lobbies = new List<LobbyInfo>();

            for (int i = 0; i < result.lobbies.Count; i++)
            {
                var l = result.lobbies[i];
                lobbies.Add(new LobbyInfo
                {
                    id = l.id,
                    name = l.name,
                    code = l.code,
                    maxPlayers = l.maxPlayers,
                });
            }

            return LobbyCollectionResponse.Success(lobbies);
        }
    }
}
