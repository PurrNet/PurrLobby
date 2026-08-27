using System.Threading.Tasks;
using UnityEngine;
#if NAKAMA
using System;
using System.Collections.Generic;
using Nakama;
#else
// Serialized fields stay declared so preset assets keep their values without the SDK.
#pragma warning disable CS0414
#endif

namespace PurrNet.Lobby.Nakama
{
    [ProviderDependency("com.heroiclabs.nakama-unity", "Nakama Unity")]
    [CreateAssetMenu(menuName = "PurrLobby/Nakama/Matchmaker", order = -202)]
    public class NakamaMatchmakingProvider : MatchmakingProvider
    {
        [Tooltip("Minimum number of users that must match before Nakama forms a match.")]
        [SerializeField] private int _minCount = 2;

        [Tooltip("Maximum number of users in a single match.")]
        [SerializeField] private int _maxCount = 4;

#if NAKAMA
        private string _activeNakamaTicketId;
        private bool _matchmakerSubscribed;

        public override Task Initialize()
        {
            // Detach any handler left over from a previous menu boot before
            // resubscribing — Initialize runs on every return to the menu.
            UnsubscribeMatchmaker();
            _activeNakamaTicketId = null;
            EnsureMatchmakerSubscribed();
            return Task.CompletedTask;
        }

        public override async Task Logout()
        {
            await CancelActiveTicketAsync();
            UnsubscribeMatchmaker();
        }

        /// <summary>Silently drops the active ticket (no status events), cancelling it server-side.</summary>
        private async Task CancelActiveTicketAsync()
        {
            var nakamaTicket = _activeNakamaTicketId;
            _activeNakamaTicketId = null;

            if (activeTicket is { } ticket)
                TryConsumeActiveTicket(ticket);

            if (string.IsNullOrEmpty(nakamaTicket))
                return;

            try
            {
                var socket = NakamaConnection.instance.socket;
                if (socket is { IsConnected: true })
                    await socket.RemoveMatchmakerAsync(nakamaTicket);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{name}] Failed to cancel matchmaker ticket: {ex.Message}");
            }
        }

        public override async Task<MatchmakingTicketResponse> StartMatchmaking(MatchmakingRequest request)
        {
            try
            {
                var conn = NakamaConnection.instance;
                if (!conn.isSocketConnected)
                    return MatchmakingTicketResponse.Failure("Nakama socket is not connected.");

                // A previous ticket would keep matching server-side; drop it first.
                if (!string.IsNullOrEmpty(_activeNakamaTicketId))
                    await CancelActiveTicketAsync();

                EnsureMatchmakerSubscribed();

                var (query, stringProps, numericProps) = BuildMatchmakerQuery(request);
                var ticket = await conn.socket.AddMatchmakerAsync(query, _minCount, _maxCount, stringProps, numericProps);

                var publicTicket = BeginTicket();
                _activeNakamaTicketId = ticket.Ticket;

                return MatchmakingTicketResponse.Success(publicTicket);
            }
            catch (Exception ex)
            {
                return MatchmakingTicketResponse.Failure(ex.Message);
            }
        }

        public override async Task<APIResponse> CancelMatchmaking(MatchmakingTicket ticket)
        {
            if (!TryConsumeActiveTicket(ticket))
                return APIResponse.Failure("No active matchmaking with that ticket.");

            var nakamaTicket = _activeNakamaTicketId;
            _activeNakamaTicketId = null;

            try
            {
                if (!string.IsNullOrEmpty(nakamaTicket))
                    await NakamaConnection.instance.socket.RemoveMatchmakerAsync(nakamaTicket);

                CancelLocally(ticket);
                return APIResponse.Success();
            }
            catch (Exception ex)
            {
                return APIResponse.Failure(ex.Message);
            }
        }

        private void EnsureMatchmakerSubscribed()
        {
            if (_matchmakerSubscribed)
                return;
            var socket = NakamaConnection.instance.socket;
            if (socket == null)
                return;
            socket.ReceivedMatchmakerMatched += OnMatchmakerMatched;
            _matchmakerSubscribed = true;
        }

        private void UnsubscribeMatchmaker()
        {
            if (!_matchmakerSubscribed)
                return;
            var socket = NakamaConnection.instance.socket;
            if (socket != null)
                socket.ReceivedMatchmakerMatched -= OnMatchmakerMatched;
            _matchmakerSubscribed = false;
        }

        private void OnMatchmakerMatched(IMatchmakerMatched matched)
        {
            if (matched == null || activeTicket is not { } publicTicket)
                return;

            if (!string.IsNullOrEmpty(_activeNakamaTicketId) && matched.Ticket != _activeNakamaTicketId)
                return;

            _activeNakamaTicketId = null;

            // Consume the ticket synchronously so a cancel landing during the async
            // join fails cleanly instead of abandoning a silently-joined match.
            TryConsumeActiveTicket(publicTicket);

            HandleMatchedAsync(publicTicket, matched).Forget($"[{name}] Matchmaker join failed");
        }

        private async Task HandleMatchedAsync(MatchmakingTicket publicTicket, IMatchmakerMatched matched)
        {
            try
            {
                var conn = NakamaConnection.instance;

                var match = await conn.socket.JoinMatchAsync(matched);

                var hostUserId = ResolveLowestUserId(matched);
                var isHost = hostUserId == conn.session.UserId;

                CompleteMatch(publicTicket, new MatchResult
                {
                    isHost = isHost,
                    connection = new ConnectionInfo
                    {
                        serverAddress = match.Id,
                        hostId = hostUserId,
                    },
                });
            }
            catch (Exception ex)
            {
                FailMatch(publicTicket, ex.Message);
            }
        }

        private static string ResolveLowestUserId(IMatchmakerMatched matched)
        {
            string best = matched.Self?.Presence?.UserId;
            if (matched.Users != null)
            {
                foreach (var u in matched.Users)
                {
                    var userId = u?.Presence?.UserId;
                    if (string.IsNullOrEmpty(userId))
                        continue;
                    if (string.IsNullOrEmpty(best) || string.CompareOrdinal(userId, best) < 0)
                        best = userId;
                }
            }
            return best;
        }

        /// <summary>Translates a <see cref="MatchmakingRequest"/> into a Nakama Lucene query and property bags.</summary>
        private static (string query, Dictionary<string, string> stringProps, Dictionary<string, double> numericProps) BuildMatchmakerQuery(MatchmakingRequest request)
        {
            var stringProps = new Dictionary<string, string>();
            var numericProps = new Dictionary<string, double>();
            var queryParts = new List<string>();

            if (!string.IsNullOrEmpty(request.gameMode))
            {
                stringProps["gameMode"] = request.gameMode;
                queryParts.Add($"+properties.gameMode:{EscapeLuceneTerm(request.gameMode)}");
            }

            if (request.attributes != null)
            {
                foreach (var kvp in request.attributes)
                {
                    if (string.IsNullOrEmpty(kvp.Key))
                        continue;

                    if (double.TryParse(kvp.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var num))
                    {
                        numericProps[kvp.Key] = num;
                        queryParts.Add($"+properties.{kvp.Key}:{num.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    }
                    else
                    {
                        stringProps[kvp.Key] = kvp.Value;
                        queryParts.Add($"+properties.{kvp.Key}:{EscapeLuceneTerm(kvp.Value)}");
                    }
                }
            }

            var query = queryParts.Count == 0 ? "*" : string.Join(" ", queryParts);
            return (query, stringProps, numericProps);
        }

        private static string EscapeLuceneTerm(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "\"\"";

            bool needsQuoting = false;
            foreach (var c in raw)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                {
                    needsQuoting = true;
                    break;
                }
            }

            if (!needsQuoting)
                return raw;

            return $"\"{raw.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
        }
#else
        private const string NakamaUnavailable =
            "Nakama Unity is not installed. Install it from the LobbyManager inspector or the PurrNet Packages window.";

        public override Task<MatchmakingTicketResponse> StartMatchmaking(MatchmakingRequest request) =>
            Task.FromResult(MatchmakingTicketResponse.Failure(NakamaUnavailable));

        public override Task<APIResponse> CancelMatchmaking(MatchmakingTicket ticket) =>
            Task.FromResult(APIResponse.Failure(NakamaUnavailable));
#endif
    }
}
