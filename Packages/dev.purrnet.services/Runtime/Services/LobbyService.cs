using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PurrNet.Services
{
    public class LobbyService
    {
        readonly ServiceHttp _http;
        readonly Func<string> _getApiKey;
        readonly Func<string> _getSessionToken;
        readonly Func<string> _getServerUrl;

        internal LobbyService(
            ServiceHttp http,
            Func<string> getApiKey,
            Func<string> getSessionToken,
            Func<string> getServerUrl)
        {
            _http = http;
            _getApiKey = getApiKey;
            _getSessionToken = getSessionToken;
            _getServerUrl = getServerUrl;
        }

        public async Task<LobbyResult> CreateAsync(CreateLobbyOptions? options = null)
        {
            var request = new CreateLobbyRequest();
            var opts = options ?? CreateLobbyOptions.Default;

            request.name = opts.name;
            request.maxPlayers = opts.maxPlayers;
            request.visibility = opts.visibility;
            request.metadata = opts.metadata;

            var response = await _http.PostAsync<CreateLobbyResponse>("/api/lobby", request);

            if (!response.success)
            {
                return new LobbyResult
                {
                    success = false,
                    error = response.error
                };
            }

            var data = response.data;
            return new LobbyResult
            {
                success = true,
                playerToken = data.playerToken,
                lobby = new LobbyData
                {
                    id = data.id,
                    name = data.name,
                    ownerId = data.ownerId,
                    hostPlayerId = data.hostPlayerId,
                    maxPlayers = data.maxPlayers,
                    visibility = data.visibility,
                    joinable = data.joinable,
                    code = data.code,
                    chatSeq = data.chatSeq,
                    version = data.version,
                    createdAt = data.createdAt
                }
            };
        }

        public async Task<LobbyListResult> ListAsync(Dictionary<string, string> filter = null)
        {
            if (filter != null && filter.Count > 0)
            {
                var query = new LobbyQuery();
                foreach (var kvp in filter)
                    query.AddStringFilter(kvp.Key, LobbyComparison.Equal, kvp.Value);
                return await ListAsync(query);
            }

            return await ListAsync((LobbyQuery)null);
        }

        public async Task<LobbyListResult> ListAsync(LobbyQuery query)
        {
            var path = "/api/lobby";

            if (query != null)
                path += query.ToQueryString();

            var response = await _http.GetAsync<LobbyListResponse>(path);

            if (!response.success)
            {
                return new LobbyListResult
                {
                    success = false,
                    error = response.error
                };
            }

            return new LobbyListResult
            {
                success = true,
                lobbies = response.data.lobbies
            };
        }

        public async Task<JoinResult> JoinAsync(string lobbyId)
        {
            var response = await _http.PostAsync<JoinResponse>($"/api/lobby/{lobbyId}/join");

            if (!response.success)
            {
                return new JoinResult
                {
                    success = false,
                    error = response.error
                };
            }

            return new JoinResult
            {
                success = true,
                lobbyId = lobbyId,
                playerToken = response.data.playerToken
            };
        }

        public async Task<JoinResult> JoinByCodeAsync(string code)
        {
            var request = new JoinByCodeRequest { code = code };
            var response = await _http.PostAsync<JoinResponse>("/api/lobby/join-by-code", request);

            if (!response.success)
            {
                return new JoinResult
                {
                    success = false,
                    error = response.error
                };
            }

            return new JoinResult
            {
                success = true,
                lobbyId = response.data.lobbyId,
                playerToken = response.data.playerToken
            };
        }

        public async Task<JoinResult> QuickJoinAsync(Dictionary<string, string> filter = null)
        {
            if (filter != null && filter.Count > 0)
            {
                var query = new LobbyQuery();
                foreach (var kvp in filter)
                    query.AddStringFilter(kvp.Key, LobbyComparison.Equal, kvp.Value);
                return await QuickJoinAsync(query);
            }

            return await QuickJoinAsync((LobbyQuery)null);
        }

        public async Task<JoinResult> QuickJoinAsync(LobbyQuery query)
        {
            object request;
            if (query != null)
                request = query.ToQuickJoinBody();
            else
                request = new QuickJoinRequest();

            var response = await _http.PostAsync<JoinResponse>("/api/lobby/quick-join", request);

            if (!response.success)
            {
                return new JoinResult
                {
                    success = false,
                    error = response.error
                };
            }

            return new JoinResult
            {
                success = true,
                lobbyId = response.data.lobbyId,
                playerToken = response.data.playerToken
            };
        }

        public async Task<ServiceResult> LeaveAsync(string lobbyId)
        {
            var response = await _http.PostAsync<SuccessResponse>($"/api/lobby/{lobbyId}/leave");

            return new ServiceResult
            {
                success = response.success,
                error = response.error
            };
        }

        public async Task<ServiceResult> DestroyAsync(string lobbyId)
        {
            var response = await _http.DeleteAsync<SuccessResponse>($"/api/lobby/{lobbyId}");

            return new ServiceResult
            {
                success = response.success,
                error = response.error
            };
        }

        public async Task<ServiceResult> KickAsync(string lobbyId, string playerId)
        {
            var request = new KickRequest { playerId = playerId };
            var response = await _http.PostAsync<SuccessResponse>($"/api/lobby/{lobbyId}/kick", request);

            return new ServiceResult
            {
                success = response.success,
                error = response.error
            };
        }

        public async Task<ServiceResult> SetJoinableAsync(string lobbyId, bool joinable)
        {
            var request = new SetJoinableRequest { joinable = joinable };
            var response = await _http.PostAsync<SuccessResponse>($"/api/lobby/{lobbyId}/start", request);

            return new ServiceResult
            {
                success = response.success,
                error = response.error
            };
        }

        public async Task<LobbySnapshotResult> PollAsync(string lobbyId, int? chatAfterSeq = null)
        {
            var path = $"/api/lobby/{lobbyId}";

            if (chatAfterSeq.HasValue)
                path += $"?chatAfterSeq={chatAfterSeq.Value}";

            var response = await _http.GetAsync<LobbySnapshot>(path);

            if (!response.success)
            {
                return new LobbySnapshotResult
                {
                    success = false,
                    error = response.error
                };
            }

            return new LobbySnapshotResult
            {
                success = true,
                snapshot = response.data
            };
        }

        public async Task<ServiceResult> SetMetadataAsync(string lobbyId, Dictionary<string, string> metadata)
        {
            var request = new MetadataRequest { metadata = metadata };
            var response = await _http.PatchAsync<SuccessResponse>($"/api/lobby/{lobbyId}/metadata", request);

            return new ServiceResult
            {
                success = response.success,
                error = response.error
            };
        }

        public async Task<ServiceResult> SetPlayerMetadataAsync(string lobbyId, Dictionary<string, string> metadata)
        {
            var request = new MetadataRequest { metadata = metadata };
            var response = await _http.PatchAsync<SuccessResponse>($"/api/lobby/{lobbyId}/player", request);

            return new ServiceResult
            {
                success = response.success,
                error = response.error
            };
        }

        public async Task<ChatResult> SendChatAsync(string lobbyId, byte[] data)
        {
            var request = new ChatRequest { data = Convert.ToBase64String(data) };
            var response = await _http.PostAsync<ChatSendResponse>($"/api/lobby/{lobbyId}/chat", request);

            if (!response.success)
            {
                return new ChatResult
                {
                    success = false,
                    error = response.error
                };
            }

            return new ChatResult
            {
                success = true,
                seq = response.data.seq
            };
        }

        public LobbyConnection Connect(string lobbyId, string playerToken)
        {
            var serverUrl = _getServerUrl();
            var wsUrl = serverUrl.Replace("https://", "wss://").Replace("http://", "ws://").TrimEnd('/');
            var uri = new Uri($"{wsUrl}/ws/lobby/{lobbyId}");

            return new LobbyConnection(
                uri,
                _getApiKey(),
                _getSessionToken(),
                playerToken
            );
        }
    }
}
