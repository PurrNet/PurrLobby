#if NAKAMA
using System;
using System.Threading.Tasks;
using Nakama;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby.Nakama
{
    /// <summary>
    /// Lobby provider backed by Nakama relayed matches. Only supports create, join-by-id, and
    /// join-by-code (which maps to the match id). Browse and random-join are unsupported because
    /// Nakama relayed-match labels are immutable after creation. Use <see cref="NakamaMatchmakingProvider"/>
    /// for discovery instead.
    /// </summary>
    [CreateAssetMenu(menuName = "PurrLobby/Nakama/Lobby Provider", order = -202)]
    public class NakamaLobbyProvider : LobbyProvider
    {
        [SerializeField] private NakamaSessionProvider _sessionProvider;

        [Tooltip("Default max players for newly created lobbies.")]
        [SerializeField] private int _maxPlayers = 4;

        [Tooltip("How long a joiner waits for the lobby owner's first snapshot before failing the join, in milliseconds.")]
        [SerializeField] private int _snapshotTimeoutMs = 4000;

        public override int maxPlayer => _maxPlayers;

        public override LobbyCapabilities capabilities =>
            LobbyCapabilities.CreateLobby | LobbyCapabilities.JoinLobbyById | LobbyCapabilities.JoinLobbyByCode;

        public override async Task Login(ViewStack stack)
        {
            if (_sessionProvider == null)
            {
                Debug.LogError($"[{name}] NakamaSessionProvider is not assigned.");
                return;
            }
            await _sessionProvider.Login(stack);
        }

        public override void Logout()
        {
            if (_sessionProvider != null)
                _ = _sessionProvider.Logout();
        }

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

        public override Task<LobbyCollectionResponse> QueryLobbies(LobbyQuery query = null) =>
            Task.FromResult(LobbyCollectionResponse.Failure("NakamaLobbyProvider does not support lobby listing. Discovery requires a custom server module which this provider intentionally avoids."));

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
    }
}
#endif
