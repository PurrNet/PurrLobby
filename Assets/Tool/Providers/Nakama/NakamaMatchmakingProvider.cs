#if NAKAMA
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nakama;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby.Nakama
{
    [CreateAssetMenu(menuName = "PurrLobby/Nakama/Matchmaker", order = -202)]
    public class NakamaMatchmakingProvider : MatchmakingProvider
    {
        [SerializeField] private NakamaSessionProvider _sessionProvider;

        [Tooltip("Minimum number of users that must match before Nakama forms a match.")]
        [SerializeField] private int _minCount = 2;

        [Tooltip("Maximum number of users in a single match.")]
        [SerializeField] private int _maxCount = 4;

        [Tooltip("How long a matchmade non-host client waits for the host's first snapshot before proceeding without it, in milliseconds.")]
        [SerializeField] private int _snapshotTimeoutMs = 4000;

        private MatchmakingTicket? _activeTicket;
        private string _activeNakamaTicketId;
        private bool _matchmakerSubscribed;

        public override async Task Login(ViewStack stack)
        {
            if (_sessionProvider == null)
            {
                Debug.LogError($"[{name}] NakamaSessionProvider is not assigned.");
                return;
            }

            ResetState();
            await _sessionProvider.Login(stack);
            EnsureMatchmakerSubscribed();
        }

        public override void Logout()
        {
            CancelActiveTicketSilently();
            UnsubscribeMatchmaker();
        }

        private void ResetState()
        {
            _matchmakerSubscribed = false;
            _activeTicket = null;
            _activeNakamaTicketId = null;
        }

        private async void CancelActiveTicketSilently()
        {
            var nakamaTicket = _activeNakamaTicketId;
            _activeTicket = null;
            _activeNakamaTicketId = null;

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
                Debug.LogWarning($"[{name}] Failed to cancel matchmaker ticket on logout: {ex.Message}");
            }
        }

        public override async void StartMatchmaking(MatchmakingRequest request, Action<MatchmakingTicketResponse> onComplete)
        {
            try
            {
                var conn = NakamaConnection.instance;
                if (!conn.isSocketConnected)
                {
                    onComplete?.Invoke(MatchmakingTicketResponse.Failure("Nakama socket is not connected."));
                    return;
                }

                EnsureMatchmakerSubscribed();

                var (query, stringProps, numericProps) = BuildMatchmakerQuery(request);
                var ticket = await conn.socket.AddMatchmakerAsync(query, _minCount, _maxCount, stringProps, numericProps);

                var publicTicket = new MatchmakingTicket { ticketId = Guid.NewGuid().ToString() };
                _activeTicket = publicTicket;
                _activeNakamaTicketId = ticket.Ticket;

                onComplete?.Invoke(MatchmakingTicketResponse.Success(publicTicket));
                RaiseStatusChanged(publicTicket, MatchmakingStatus.Searching);
            }
            catch (Exception ex)
            {
                onComplete?.Invoke(MatchmakingTicketResponse.Failure(ex.Message));
            }
        }

        public override async void CancelMatchmaking(MatchmakingTicket ticket, Action<APIResponse> onComplete)
        {
            if (_activeTicket == null || _activeTicket.Value.ticketId != ticket.ticketId)
            {
                onComplete?.Invoke(APIResponse.Failure("No active matchmaking with that ticket."));
                return;
            }

            var nakamaTicket = _activeNakamaTicketId;
            var publicTicket = _activeTicket.Value;
            _activeTicket = null;
            _activeNakamaTicketId = null;

            try
            {
                if (!string.IsNullOrEmpty(nakamaTicket))
                    await NakamaConnection.instance.socket.RemoveMatchmakerAsync(nakamaTicket);

                RaiseStatusChanged(publicTicket, MatchmakingStatus.Cancelled);
                onComplete?.Invoke(APIResponse.Success());
            }
            catch (Exception ex)
            {
                onComplete?.Invoke(APIResponse.Failure(ex.Message));
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

        private async void OnMatchmakerMatched(IMatchmakerMatched matched)
        {
            if (matched == null || _activeTicket == null)
                return;

            if (!string.IsNullOrEmpty(_activeNakamaTicketId) && matched.Ticket != _activeNakamaTicketId)
                return;

            var publicTicket = _activeTicket.Value;
            _activeTicket = null;
            _activeNakamaTicketId = null;

            try
            {
                var conn = NakamaConnection.instance;
                var match = await conn.socket.JoinMatchAsync(matched);

                var hostUserId = ResolveLowestUserId(matched);

                var lobby = new NakamaLobby(conn.session,
                    conn.socket,
                    match,
                    code: string.Empty,
                    name: "Matchmade",
                    maxPlayers: _maxCount,
                    hostUserId: hostUserId,
                    initialMetadata: null);

                var isHost = hostUserId == conn.session.UserId;

                if (!isHost)
                {
                    try
                    {
                        await lobby.AwaitFirstSnapshotAsync(_snapshotTimeoutMs);
                    }
                    catch (Exception snapshotEx)
                    {
                        Debug.LogWarning($"[{name}] Proceeding without host snapshot: {snapshotEx.Message}");
                    }
                }

                RaiseStatusChanged(publicTicket, MatchmakingStatus.Found);
                RaiseMatchFound(publicTicket, new MatchResult { lobby = lobby, isHost = isHost });
            }
            catch (Exception ex)
            {
                RaiseStatusChanged(publicTicket, MatchmakingStatus.Failed);
                RaiseMatchmakingError(publicTicket, ex.Message);
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
            // Quote the term to handle whitespace and special chars; escape embedded quotes.
            return $"\"{raw.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
        }
    }
}
#endif
