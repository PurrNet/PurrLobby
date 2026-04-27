#if NAKAMA
using System;
using System.Threading.Tasks;
using Nakama;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby.Nakama
{
    /// <summary>
    /// Nakama lobby provider. Nakama has no built-in lobby directory, and discovery features
    /// (browse / join-by-code / random-join) require a custom server-side match handler module.
    /// To stay plugin-free this provider only exposes the operations Nakama supports out of the
    /// box: creating a relayed match (used as a lobby) and joining one by its known match id.
    /// Code-based pairing should be done through <see cref="NakamaMatchmakingProvider"/>, which
    /// uses Nakama's native matchmaker.
    /// </summary>
    [CreateAssetMenu(menuName = "PurrLobby/Nakama/Lobby Provider", order = -202)]
    public class NakamaLobbyProvider : LobbyProvider
    {
        [SerializeField] private NakamaSessionProvider _sessionProvider;

        [Tooltip("Default max players for newly created lobbies.")]
        [SerializeField] private int _maxPlayers = 4;

        [Tooltip("How long to wait for the host's snapshot after joining a lobby, in milliseconds.")]
        [SerializeField] private int _snapshotTimeoutMs = 4000;

        public override int maxPlayer => _maxPlayers;

        public override LobbyCapabilities capabilities =>
            LobbyCapabilities.CreateLobby | LobbyCapabilities.JoinLobbyById;

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

            try
            {
                // Relayed match — no server module required.
                var match = await conn.socket.CreateMatchAsync();
                var maxPlayers = settings.maxPlayers > 0 ? settings.maxPlayers : _maxPlayers;

                var lobby = new NakamaLobby(conn.session,
                    conn.socket,
                    match,
                    code: string.Empty,
                    name: string.IsNullOrEmpty(settings.name) ? "Lobby" : settings.name,
                    maxPlayers: maxPlayers,
                    hostUserId: conn.session.UserId,
                    initialMetadata: settings.metadata);

                return LobbyResponse.Success(lobby);
            }
            catch (Exception ex)
            {
                return LobbyResponse.Failure($"Failed to create Nakama match: {ex.Message}");
            }
        }

        public override async Task<LobbyResponse> JoinLobby(string lobbyId)
        {
            if (string.IsNullOrEmpty(lobbyId))
                return LobbyResponse.Failure("Lobby id is empty.");

            if (!TryGetReadyConnection(out var conn, out var error))
                return LobbyResponse.Failure(error);

            try
            {
                var match = await conn.socket.JoinMatchAsync(lobbyId);
                var hostHint = ResolveHostHint(match, conn.session.UserId);

                var lobby = new NakamaLobby(conn.session,
                    conn.socket,
                    match,
                    code: string.Empty,
                    name: string.Empty,
                    maxPlayers: 0,
                    hostUserId: hostHint,
                    initialMetadata: null);

                await lobby.AwaitFirstSnapshotAsync(_snapshotTimeoutMs);
                return LobbyResponse.Success(lobby);
            }
            catch (Exception ex)
            {
                return LobbyResponse.Failure($"Failed to join Nakama match: {ex.Message}");
            }
        }

        public override Task<LobbyResponse> JoinLobbyByCode(string code) =>
            Task.FromResult(LobbyResponse.Failure("NakamaLobbyProvider does not support join-by-code. Use the matchmaking provider for code-based pairing."));

        public override Task<LobbyResponse> JoinRandom(LobbyQuery query = null) =>
            Task.FromResult(LobbyResponse.Failure("NakamaLobbyProvider does not support random join. Use the matchmaking provider instead."));

        public override Task<LobbyCollectionResponse> QueryLobbies(LobbyQuery query = null) =>
            Task.FromResult(LobbyCollectionResponse.Failure("NakamaLobbyProvider does not support lobby listing. Discovery requires a custom server module which this provider intentionally avoids."));

        private static string ResolveHostHint(IMatch match, string selfId)
        {
            string best = selfId;
            if (match.Presences != null)
            {
                foreach (var p in match.Presences)
                {
                    if (p == null || string.IsNullOrEmpty(p.UserId))
                        continue;
                    if (string.IsNullOrEmpty(best) || string.CompareOrdinal(p.UserId, best) < 0)
                        best = p.UserId;
                }
            }
            return best;
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
    }
}
#endif
