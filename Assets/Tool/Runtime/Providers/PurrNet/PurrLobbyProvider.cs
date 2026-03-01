using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PurrLobby.Internal;

namespace PurrLobby
{
    public sealed class PurrLobbyProvider : ILobbyProvider
    {
        readonly LobbyApiClient _api;
        readonly string _playerId;
        readonly string _playerName;

        public PurrLobbyProvider(string apiUrl, string apiKey, string gameId, string playerId, string playerName)
        {
            if (string.IsNullOrEmpty(apiUrl)) throw new ArgumentException("apiUrl is required");
            if (string.IsNullOrEmpty(playerId)) throw new ArgumentException("playerId is required");
            if (string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(gameId))
                throw new ArgumentException("Either apiKey or gameId is required");

            _playerId = playerId;
            _playerName = string.IsNullOrEmpty(playerName) ? playerId : playerName;
            _api = new LobbyApiClient(apiUrl, apiKey, gameId, playerId, _playerName);
        }

        public async Task<ILobby> CreateLobby(LobbySettings settings)
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

            var lobby = new PurrLobby(_api, lobbyId, _playerId, lobbyData);

            // Fetch full snapshot to populate players, metadata, chat
            var snapshot = await FetchSnapshot(lobbyId);
            lobby.ApplyInitialSnapshot(snapshot);

            return lobby;
        }

        public async Task<ILobby> JoinLobby(string lobbyId)
        {
            await _api.PostAsync($"/api/lobby/{lobbyId}/join");
            return await BuildLobbyFromPoll(lobbyId);
        }

        public async Task<ILobby> JoinLobbyByCode(string code)
        {
            string response = await _api.PostAsync("/api/lobby/join-by-code", new Dictionary<string, object>
            {
                { "code", code }
            });

            var result = Json.ParseObject(response);
            string lobbyId = result.GetString("lobbyId");
            return await BuildLobbyFromPoll(lobbyId);
        }

        public async Task<ILobby> JoinRandom(LobbyQuery query = default)
        {
            var body = new Dictionary<string, object>();
            if (query.dataFilters != null && query.dataFilters.Count > 0)
                body["filter"] = query.dataFilters;

            string response = await _api.PostAsync("/api/lobby/quick-join", body.Count > 0 ? body : null);
            var result = Json.ParseObject(response);
            string lobbyId = result.GetString("lobbyId");
            return await BuildLobbyFromPoll(lobbyId);
        }

        public async Task<IReadOnlyList<LobbyInfo>> QueryLobbies(LobbyQuery query = default)
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

        async Task<JObject> FetchSnapshot(string lobbyId)
        {
            string response = await _api.GetAsync($"/api/lobby/{lobbyId}");
            return Json.ParseObject(response);
        }

        async Task<ILobby> BuildLobbyFromPoll(string lobbyId)
        {
            var snapshot = await FetchSnapshot(lobbyId);
            var lobbyObj = snapshot.GetObject("lobby");

            var lobby = new PurrLobby(_api, lobbyId, _playerId, lobbyObj);
            lobby.ApplyInitialSnapshot(snapshot);
            return lobby;
        }
    }
}
