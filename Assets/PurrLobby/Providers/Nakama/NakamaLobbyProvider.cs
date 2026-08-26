using System.Threading.Tasks;
using UnityEngine;
#if NAKAMA
using System;
using System.Collections.Generic;
using Nakama;
#else
// Serialized fields stay declared so preset assets keep their values without the SDK.
#pragma warning disable CS0414
#pragma warning disable CS0169
#endif

namespace PurrNet.Lobby.Nakama
{
    /// <summary>
    /// Lobby provider backed by Nakama relayed matches. Supports create, join-by-id,
    /// join-by-code (which maps to the match id), and a minimal lobby browser.
    /// Browsing is intentionally bare: relayed matches expose no name, metadata, or max size
    /// without a custom server module (which this provider avoids), so entries show the match id
    /// as the name and query filters are ignored. Extend QueryLobbies with your own server RPC
    /// if you need richer listings. Random-join is unsupported; use <see cref="NakamaMatchmakingProvider"/>.
    /// Every relayed match is always listed, so private lobbies are unsupported and
    /// <see cref="LobbySettings.visibility"/> is ignored.
    /// </summary>
    [ProviderDependency("com.heroiclabs.nakama-unity", "Nakama Unity")]
    [CreateAssetMenu(menuName = "PurrLobby/Nakama/Lobby Provider", order = -202)]
    public class NakamaLobbyProvider : LobbyProvider
    {
        [SerializeField] private NakamaSessionProvider _sessionProvider;

        [Tooltip("Default max players for newly created lobbies.")]
        [SerializeField] private int _maxPlayers = 4;

        [Tooltip("How long a joiner waits for the lobby owner's first snapshot before failing the join, in milliseconds.")]
        [SerializeField] private int _snapshotTimeoutMs = 4000;

        [Tooltip("Max matches returned by the lobby browser.")]
        [SerializeField] private int _queryLimit = 100;

        public override int maxPlayer => _maxPlayers;

#if NAKAMA
        public override LobbyCapabilities capabilities =>
            LobbyCapabilities.CreateLobby | LobbyCapabilities.JoinLobbyById | LobbyCapabilities.JoinLobbyByCode |
            LobbyCapabilities.QueryLobbies;

        public override async Task<LobbyResponse> CreateLobby(LobbySettings settings)
        {
            if (!TryGetReadyConnection(out var conn, out var error))
                return LobbyResponse.Failure(error);

            IMatch match;
            try
            {
                match = await conn.socket.CreateMatchAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NakamaLobbyProvider] CreateMatchAsync threw {ex.GetType().FullName}: {ex}\nsocket.IsConnected={conn.socket?.IsConnected}, session.IsExpired={conn.session?.IsExpired}");
                return LobbyResponse.Failure($"Failed to create Nakama match ({ex.GetType().Name}): {ex.Message}");
            }

            try
            {
                var maxPlayers = settings.maxPlayers > 0 ? settings.maxPlayers : _maxPlayers;

                var lobby = new NakamaLobby(conn.session,
                    conn.socket,
                    match,
                    maxPlayers: maxPlayers,
                    hostUserId: conn.session.UserId,
                    initialMetadata: settings.metadata);

                return LobbyResponse.Success(lobby);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NakamaLobbyProvider] NakamaLobby ctor threw {ex.GetType().FullName}: {ex}");
                return LobbyResponse.Failure($"Failed to wrap Nakama match ({ex.GetType().Name}): {ex.Message}");
            }
        }

        public override async Task<LobbyResponse> JoinLobby(string lobbyId)
        {
            if (string.IsNullOrEmpty(lobbyId))
                return LobbyResponse.Failure("Lobby id is empty.");

            if (!TryGetReadyConnection(out var conn, out var error))
                return LobbyResponse.Failure(error);

            IMatch match;
            try
            {
                match = await conn.socket.JoinMatchAsync(lobbyId);
            }
            catch (Exception ex)
            {
                return LobbyResponse.Failure($"Failed to join Nakama match: {ex.Message}");
            }

            NakamaLobby lobby = null;
            try
            {
                lobby = new NakamaLobby(conn.session,
                    conn.socket,
                    match,
                    maxPlayers: 0,
                    hostUserId: null,
                    initialMetadata: null);

                await lobby.AwaitFirstSnapshotAsync(_snapshotTimeoutMs);
                return LobbyResponse.Success(lobby);
            }
            catch (Exception ex)
            {
                lobby?.Dispose();
                try { await conn.socket.LeaveMatchAsync(match.Id); } catch { /* ignored */ }
                return LobbyResponse.Failure($"Failed to join Nakama match: {ex.Message}");
            }
        }

        public override Task<LobbyResponse> JoinLobbyByCode(string code) => JoinLobby(code);

        public override Task<LobbyResponse> JoinRandom(LobbyQuery query = null) =>
            Task.FromResult(LobbyResponse.Failure("NakamaLobbyProvider does not support random join. Use the matchmaking provider instead."));

        /// <summary>
        /// Lists open relayed matches. Without a custom server module Nakama exposes only the
        /// match id and player count, so <see cref="LobbyQuery"/> filters are ignored, the match id
        /// doubles as the name/code, and maxPlayers falls back to this provider's default.
        /// </summary>
        public override async Task<LobbyCollectionResponse> QueryLobbies(LobbyQuery query = null)
        {
            if (!TryGetReadyConnection(out var conn, out var error))
                return LobbyCollectionResponse.Failure(error);

            IApiMatchList matchList;
            try
            {
                matchList = await conn.client.ListMatchesAsync(conn.session,
                    0, int.MaxValue, _queryLimit, false, null, null);
            }
            catch (Exception ex)
            {
                return LobbyCollectionResponse.Failure($"Failed to list Nakama matches: {ex.Message}");
            }

            var lobbies = new List<LobbyInfo>();

            foreach (var match in matchList.Matches)
            {
                if (match.Authoritative)
                    continue;

                lobbies.Add(new LobbyInfo
                {
                    id = match.MatchId,
                    code = match.MatchId,
                    name = match.MatchId,
                    playerCount = match.Size,
                    maxPlayers = _maxPlayers,
                    joinable = true,
                    metadata = new Dictionary<string, string>()
                });
            }

            return LobbyCollectionResponse.Success(lobbies);
        }

        private bool TryGetReadyConnection(out NakamaConnection conn, out string error)
        {
            conn = NakamaConnection.instance;

            if (_sessionProvider == null)
            {
                error = "NakamaSessionProvider is not assigned on the lobby provider.";
                return false;
            }

            if (!conn.isAuthenticated)
            {
                error = "Nakama session is not authenticated. Did you call SessionProvider.Login first?";
                return false;
            }

            if (!conn.isSocketConnected)
            {
                error = "Nakama socket is not connected. Did SessionProvider open the socket?";
                return false;
            }

            error = null;
            return true;
        }
#else
        private const string NakamaUnavailable =
            "Nakama Unity is not installed. Install it from the LobbyManager inspector or the PurrNet Packages window.";

        public override LobbyCapabilities capabilities => LobbyCapabilities.None;

        public override Task<LobbyResponse> CreateLobby(LobbySettings settings) =>
            Task.FromResult(LobbyResponse.Failure(NakamaUnavailable));

        public override Task<LobbyResponse> JoinLobby(string lobbyId) =>
            Task.FromResult(LobbyResponse.Failure(NakamaUnavailable));

        public override Task<LobbyResponse> JoinLobbyByCode(string code) =>
            Task.FromResult(LobbyResponse.Failure(NakamaUnavailable));

        public override Task<LobbyResponse> JoinRandom(LobbyQuery query = null) =>
            Task.FromResult(LobbyResponse.Failure(NakamaUnavailable));

        public override Task<LobbyCollectionResponse> QueryLobbies(LobbyQuery query = null) =>
            Task.FromResult(LobbyCollectionResponse.Failure(NakamaUnavailable));
#endif
    }
}
