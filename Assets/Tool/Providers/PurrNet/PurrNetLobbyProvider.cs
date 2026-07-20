using System.Collections.Generic;
using System.Threading.Tasks;
using PurrNet.Services;
using UnityEngine;

namespace PurrNet.Lobby.PurrNet
{
    [CreateAssetMenu(menuName = "PurrLobby/PurrNet/Lobby Provider", order = -201)]
    public class PurrNetLobbyProvider : LobbyProvider
    {
        [SerializeField] private int _maxPlayers = 4;

        public override int maxPlayer => _maxPlayers;

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

            if (result.success)
                services.activePlayerToken = result.playerToken;

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
            var result = await PurrServices.instance.lobbies.JoinAsync(lobbyId);
            return await PollAndWrap(result);
        }

        public override async Task<LobbyResponse> JoinLobbyByCode(string code)
        {
            var result = await PurrServices.instance.lobbies.JoinByCodeAsync(code);
            return await PollAndWrap(result);
        }

        public override async Task<LobbyResponse> JoinRandom(LobbyQuery query = null)
        {
            var result = await PurrServices.instance.lobbies.QuickJoinAsync(ToServicesQuery(query));
            return await PollAndWrap(result);
        }

        private async Task<LobbyResponse> PollAndWrap(JoinResult result)
        {
            if (!result.success)
                return LobbyResponse.Failure(result.error);

            var services = PurrServices.instance;
            services.activePlayerToken = result.playerToken;

            var snapshot = await services.lobbies.PollAsync(result.lobbyId);
            if (!snapshot.success)
                return LobbyResponse.Failure(snapshot.error);

            var lobby = new PurrNetLobby(services.lobbies, snapshot.snapshot.lobby, result.playerToken);
            return LobbyResponse.Success(lobby);
        }

        public override async Task<LobbyCollectionResponse> QueryLobbies(LobbyQuery query = null)
        {
            var services = PurrServices.instance;
            var result = await services.lobbies.ListAsync(ToServicesQuery(query));

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
                    playerCount = l.playerCount,
                    maxPlayers = l.maxPlayers,
                    joinable = l.joinable,
                    metadata = l.metadata
                });
            }

            return LobbyCollectionResponse.Success(lobbies);
        }

        private static global::PurrNet.Services.LobbyQuery ToServicesQuery(LobbyQuery query)
        {
            if (query == null) return null;

            var sq = new global::PurrNet.Services.LobbyQuery();

            if (query.stringFilters != null)
            {
                foreach (var f in query.stringFilters)
                    sq.AddStringFilter(f.key, ToServicesOp(f.op), f.value);
            }

            if (query.numericalFilters != null)
            {
                foreach (var f in query.numericalFilters)
                    sq.AddNumericalFilter(f.key, ToServicesOp(f.op), f.value);
            }

            if (query.nearValueSorts != null)
            {
                foreach (var ns in query.nearValueSorts)
                    sq.AddNearValueFilter(ns.key, ns.target);
            }

            if (query.slotsAvailable > 0)
                sq.SetSlotsAvailable(query.slotsAvailable);

            return sq;
        }

        private static global::PurrNet.Services.LobbyComparison ToServicesOp(FilterComparison op)
        {
            switch (op)
            {
                case FilterComparison.Equal: return global::PurrNet.Services.LobbyComparison.Equal;
                case FilterComparison.NotEqual: return global::PurrNet.Services.LobbyComparison.NotEqual;
                case FilterComparison.LessThan: return global::PurrNet.Services.LobbyComparison.LessThan;
                case FilterComparison.GreaterThan: return global::PurrNet.Services.LobbyComparison.GreaterThan;
                case FilterComparison.LessThanOrEqual: return global::PurrNet.Services.LobbyComparison.LessThanOrEqual;
                case FilterComparison.GreaterThanOrEqual: return global::PurrNet.Services.LobbyComparison.GreaterThanOrEqual;
                default: return global::PurrNet.Services.LobbyComparison.Equal;
            }
        }
    }
}
