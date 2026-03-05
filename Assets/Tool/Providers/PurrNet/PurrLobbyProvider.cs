using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PurrLobby.Utils;

namespace PurrLobby
{
    internal sealed class PurrLobbyClient
    {
        readonly LobbyApiClient _api;
        readonly PurrSession _session;

        public PurrLobbyClient(PurrSession session)
        {
            _session = session;
            if (string.IsNullOrEmpty(session.apiUrl)) throw new ArgumentException("apiUrl is required");
            if (string.IsNullOrEmpty(session.playerId)) throw new ArgumentException("playerId is required");
            if (string.IsNullOrEmpty(session.projectClientKey)) throw new ArgumentException("projectClientKey is required");
            _api = new LobbyApiClient(session);
        }

        public async Task<ILobby> CreateLobbyAsync(LobbySettings settings)
        {
            var body = new Dictionary<string, object>
            {
                { "name", settings.name ?? "Lobby" },
                { "maxPlayers", settings.maxPlayers > 0 ? settings.maxPlayers : 8 },
                { "visibility", settings.visibility == LobbyVisibility.Public ? "public" : "private" }
            };

            string response = await _api.PostAsync("/api/lobby", body);
            var lobbyData = Json.ParseObject(response);
            string lobbyId = lobbyData.GetString("id");
            string playerToken = lobbyData.GetString("playerToken");
            _session.SetPlayerToken(playerToken);

            var lobby = new PurrLobby(_api, lobbyId, _session.playerId, playerToken, lobbyData);
            var snapshot = await FetchSnapshot(lobbyId, playerToken);
            lobby.ApplyInitialSnapshot(snapshot);

            return lobby;
        }

        public async Task<ILobby> JoinLobbyAsync(string lobbyId)
        {
            string response = await _api.PostAsync($"/api/lobby/{lobbyId}/join");
            var result = Json.ParseObject(response);
            string playerToken = result.GetString("playerToken");
            return await BuildLobbyFromPoll(lobbyId, playerToken);
        }

        public async Task<ILobby> JoinLobbyByCodeAsync(string code)
        {
            string response = await _api.PostAsync("/api/lobby/join-by-code", new Dictionary<string, object>
            {
                { "code", code }
            });

            var result = Json.ParseObject(response);
            string lobbyId = result.GetString("lobbyId");
            string playerToken = result.GetString("playerToken");
            return await BuildLobbyFromPoll(lobbyId, playerToken);
        }

        public async Task<ILobby> JoinRandomAsync(LobbyQuery query = default)
        {
            var body = new Dictionary<string, object>();
            if (query.dataFilters != null && query.dataFilters.Count > 0)
                body["filter"] = query.dataFilters;

            string response = await _api.PostAsync("/api/lobby/quick-join", body.Count > 0 ? body : null);
            var result = Json.ParseObject(response);
            string lobbyId = result.GetString("lobbyId");
            string playerToken = result.GetString("playerToken");
            return await BuildLobbyFromPoll(lobbyId, playerToken);
        }

        public async Task<IReadOnlyList<LobbyInfo>> QueryLobbiesAsync(LobbyQuery query = default)
        {
            string response = await _api.GetAsync("/api/lobby");
            var data = Json.ParseObject(response);
            var lobbiesArray = data.GetArray("lobbies");
            var result = new List<LobbyInfo>();

            if (lobbiesArray == null) return result;

            foreach (var item in lobbiesArray)
            {
                if (item is not JObject obj) continue;

                var info = new LobbyInfo
                {
                    id = obj.GetString("id"),
                    name = obj.GetString("name"),
                    code = obj.GetString("code"),
                    maxPlayers = obj.GetInt("maxPlayers"),
                    playerCount = 0,
                    metadata = new Dictionary<string, string>()
                };

                result.Add(info);
            }

            if (query.maxResults > 0 && result.Count > query.maxResults)
                result.RemoveRange(query.maxResults, result.Count - query.maxResults);

            return result;
        }

        async Task<JObject> FetchSnapshot(string lobbyId, string playerToken)
        {
            string response = await _api.GetAsync($"/api/lobby/{lobbyId}", playerToken: playerToken);
            return Json.ParseObject(response);
        }

        async Task<ILobby> BuildLobbyFromPoll(string lobbyId, string playerToken)
        {
            _session.SetPlayerToken(playerToken);

            var snapshot = await FetchSnapshot(lobbyId, playerToken);
            var lobbyObj = snapshot.GetObject("lobby");

            var lobby = new PurrLobby(_api, lobbyId, _session.playerId, playerToken, lobbyObj);
            lobby.ApplyInitialSnapshot(snapshot);
            return lobby;
        }
    }
}
