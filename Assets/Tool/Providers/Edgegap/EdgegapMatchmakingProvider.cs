using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using PurrNet.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace PurrNet.Lobby.PurrNet
{
    /// <summary>
    /// Matchmaking backed by Edgegap's managed matchmaker.
    /// See https://docs.edgegap.com/learn/matchmaking.
    ///
    /// The matchmaker forms the match and deploys the game server itself, so a
    /// found match already carries a ready-to-use connection - there's no lobby
    /// and no separate allocation step. Pair this with an
    /// <see cref="EdgegapGameAllocator"/> in the orchestrator: that allocator
    /// passes the connection straight through and only loads the game scene.
    /// </summary>
    [CreateAssetMenu(menuName = "PurrLobby/Edgegap/Matchmaker", fileName = "Edgegap Matchmaker", order = -198)]
    public class EdgegapMatchmakingProvider : MatchmakingProvider
    {
        [Tooltip("Matchmaker API URL from the Edgegap dashboard, e.g. https://abc123.matchmaker.edgegap.net")]
        [SerializeField] private string _matchmakerUrl;

        [Tooltip("Matchmaker Auth Token from the dashboard. Edgegap states this token is safe to ship in a game client.")]
        [SerializeField] private string _authToken;

        [Tooltip("Profile to queue into when the matchmaking request doesn't carry a game mode of its own.")]
        [SerializeField] private string _defaultProfile = "default";

        [Tooltip("The Edgegap Game Allocator this matchmaker is paired with. Its transport and " +
                 "port name decide which assigned port the client connects through, so the two " +
                 "always agree.")]
        [SerializeField] private EdgegapGameAllocator _gameAllocator;

        [Tooltip("Milliseconds between ticket status polls. Edgegap recommends polling every 3-5 seconds.")]
        [SerializeField] private int _pollIntervalMs = 3_000;

        [Tooltip("Give up if no host is assigned within this many milliseconds.")]
        [SerializeField] private int _timeoutMs = 120_000;

        private MatchmakingTicket? _activeTicket;
        private string _activeTicketId;
        private bool _cancelled;

        public override Task Login(ViewStack stack) => Task.CompletedTask;

        public override void Logout()
        {
            _cancelled = true;
            var ticketId = _activeTicketId;
            _activeTicket = null;
            _activeTicketId = null;
            _ = DeleteTicket(ticketId);
        }

        public override async void StartMatchmaking(MatchmakingRequest request, Action<MatchmakingTicketResponse> onComplete)
        {
            try
            {
                if (string.IsNullOrEmpty(_matchmakerUrl))
                {
                    onComplete?.Invoke(MatchmakingTicketResponse.Failure($"[{name}] Matchmaker URL is not set."));
                    return;
                }

                if (_gameAllocator == null)
                {
                    onComplete?.Invoke(MatchmakingTicketResponse.Failure(
                        $"[{name}] No Edgegap Game Allocator assigned. Pair one so the matchmaker knows which transport and port to connect through."));
                    return;
                }

                _cancelled = false;
                _activeTicket = null;
                _activeTicketId = null;

                var profile = string.IsNullOrEmpty(request.gameMode) ? _defaultProfile : request.gameMode;

                MatchmakingTicket ticket;

                try
                {
                    var body = JsonConvert.SerializeObject(new TicketRequest
                    {
                        profile = profile,
                        attributes = request.attributes ?? new Dictionary<string, string>(),
                        player_ip = null
                    });

                    var post = await SendAsync(UnityWebRequest.kHttpVerbPOST, TicketsUrl(), body);
                    if (!post.ok)
                    {
                        onComplete?.Invoke(MatchmakingTicketResponse.Failure($"Failed to create ticket: {post.error}"));
                        return;
                    }

                    var created = JsonConvert.DeserializeObject<TicketResponse>(post.body);
                    if (created == null || string.IsNullOrEmpty(created.id))
                    {
                        onComplete?.Invoke(MatchmakingTicketResponse.Failure("Matchmaker returned a ticket without an id."));
                        return;
                    }

                    ticket = new MatchmakingTicket { ticketId = created.id };
                    _activeTicket = ticket;
                    _activeTicketId = created.id;

                    onComplete?.Invoke(MatchmakingTicketResponse.Success(ticket));
                    RaiseStatusChanged(ticket, MatchmakingStatus.Searching);
                }
                catch (Exception e)
                {
                    onComplete?.Invoke(MatchmakingTicketResponse.Failure(e.Message));
                    return;
                }

                await PollUntilAssigned(ticket);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                onComplete?.Invoke(MatchmakingTicketResponse.Failure($"Unexpected error: {e.Message}"));
            }
        }

        public override async void CancelMatchmaking(MatchmakingTicket ticket, Action<APIResponse> onComplete)
        {
            try
            {
                if (_activeTicket == null || _activeTicket.Value.ticketId != ticket.ticketId)
                {
                    onComplete?.Invoke(APIResponse.Failure("No active matchmaking with that ticket."));
                    return;
                }

                var ticketId = _activeTicketId;
                _activeTicket = null;
                _activeTicketId = null;
                _cancelled = true;

                var delete = await DeleteTicket(ticketId);

                RaiseStatusChanged(ticket, MatchmakingStatus.Cancelled);

                onComplete?.Invoke(delete.ok
                    ? APIResponse.Success()
                    : APIResponse.Failure(delete.error));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                onComplete?.Invoke(APIResponse.Failure($"Unexpected error: {e.Message}"));
            }
        }

        /// <summary>Polls the ticket until a host is assigned, the ticket is cancelled, or it times out.</summary>
        private async Task PollUntilAssigned(MatchmakingTicket ticket)
        {
            var elapsed = 0;

            while (!_cancelled && elapsed < _timeoutMs)
            {
                await Task.Delay(_pollIntervalMs);
                elapsed += _pollIntervalMs;

                // Cancelled, or a newer matchmaking session replaced this one.
                if (_cancelled || _activeTicket?.ticketId != ticket.ticketId)
                    return;

                (bool ok, string body, string error) poll;
                try
                {
                    poll = await SendAsync(UnityWebRequest.kHttpVerbGET, TicketUrl(ticket.ticketId), null);
                }
                catch (Exception e)
                {
                    FailTicket(ticket, e.Message);
                    return;
                }

                if (_cancelled)
                    return;

                if (!poll.ok)
                {
                    FailTicket(ticket, $"Failed to poll ticket: {poll.error}");
                    return;
                }

                TicketResponse status;
                try
                {
                    status = JsonConvert.DeserializeObject<TicketResponse>(poll.body);
                }
                catch (Exception e)
                {
                    FailTicket(ticket, $"Could not read ticket response: {e.Message}");
                    return;
                }

                switch ((status?.status ?? string.Empty).ToUpperInvariant())
                {
                    case "HOST_ASSIGNED":
                        CompleteTicket(ticket, status);
                        return;

                    case "CANCELLED":
                        _activeTicket = null;
                        _activeTicketId = null;
                        RaiseStatusChanged(ticket, MatchmakingStatus.Cancelled);
                        return;

                    default:
                        // SEARCHING, TEAM_FOUND, MATCH_FOUND - still on the way to a host.
                        RaiseStatusChanged(ticket, MatchmakingStatus.Searching);
                        break;
                }
            }

            if (!_cancelled)
                FailTicket(ticket, $"Matchmaking timed out after {_timeoutMs / 1000}s.");
        }

        private void CompleteTicket(MatchmakingTicket ticket, TicketResponse status)
        {
            _activeTicket = null;
            _activeTicketId = null;

            if (status.assignment == null)
            {
                FailTicket(ticket, "Host was assigned but the matchmaker returned no assignment.");
                return;
            }

            if (!TryBuildConnection(status.assignment, ticket.ticketId, out var connection, out var error))
            {
                FailTicket(ticket, error);
                return;
            }

            RaiseStatusChanged(ticket, MatchmakingStatus.Found);
            RaiseMatchFound(ticket, new MatchResult
            {
                lobby = null,
                connection = connection,
                isHost = false
            });
        }

        private void FailTicket(MatchmakingTicket ticket, string error)
        {
            _activeTicket = null;
            _activeTicketId = null;
            // Raise the descriptive error first: the Failed status causes the
            // matchmaking view to close and unsubscribe, so an error sent after it
            // would land on no listener.
            RaiseMatchmakingError(ticket, error);
            RaiseStatusChanged(ticket, MatchmakingStatus.Failed);
        }

        private bool TryBuildConnection(Assignment assignment, string ticketId, out ConnectionInfo connection, out string error)
        {
            connection = default;

            var address = !string.IsNullOrEmpty(assignment.fqdn) ? assignment.fqdn : assignment.public_ip;
            if (string.IsNullOrEmpty(address))
            {
                error = "Matchmaker assignment has no fqdn or public_ip.";
                return false;
            }

            if (!TryPickPort(assignment.ports, out var port, out error))
                return false;

            connection = new ConnectionInfo
            {
                serverAddress = address,
                serverPort = port.external,
                // Edgegap expects the player to hand its ticket id to the game
                // server for authentication; carry it through so the game can.
                connectionToken = ticketId
            };
            return true;
        }

        private bool TryPickPort(Dictionary<string, AssignedPort> ports, out AssignedPort port, out string error)
        {
            port = default;
            error = null;

            if (ports == null || ports.Count == 0)
            {
                error = "Matchmaker assignment exposes no ports.";
                return false;
            }

            var portName = _gameAllocator.PortName;
            if (!string.IsNullOrEmpty(portName))
            {
                if (ports.TryGetValue(portName, out port))
                    return true;

                error = $"Port `{portName}` not found on the matchmaker assignment.";
                return false;
            }

            foreach (var kv in ports)
            {
                if (MatchesTransport(kv.Value.protocol))
                {
                    port = kv.Value;
                    return true;
                }
            }

            error = $"No assigned port matched transport `{_gameAllocator.Transport}`. Set a port name explicitly on `{_gameAllocator.name}`.";
            return false;
        }

        private bool MatchesTransport(string protocol)
        {
            if (string.IsNullOrEmpty(protocol))
                return false;

            switch (_gameAllocator.Transport)
            {
                case EdgegapAllocatorTransport.UDP:
                    return protocol.Equals("UDP", StringComparison.OrdinalIgnoreCase);
                case EdgegapAllocatorTransport.Web:
                    return protocol.Equals("TCP", StringComparison.OrdinalIgnoreCase)
                        || protocol.Equals("WS", StringComparison.OrdinalIgnoreCase)
                        || protocol.Equals("WSS", StringComparison.OrdinalIgnoreCase)
                        || protocol.Equals("WEBSOCKET", StringComparison.OrdinalIgnoreCase)
                        || protocol.Equals("HTTP", StringComparison.OrdinalIgnoreCase)
                        || protocol.Equals("HTTPS", StringComparison.OrdinalIgnoreCase);
                default:
                    return false;
            }
        }

        private async Task<(bool ok, string error)> DeleteTicket(string ticketId)
        {
            if (string.IsNullOrEmpty(ticketId) || string.IsNullOrEmpty(_matchmakerUrl))
                return (true, null);

            try
            {
                var delete = await SendAsync(UnityWebRequest.kHttpVerbDELETE, TicketUrl(ticketId), null);
                return (delete.ok, delete.error);
            }
            catch (Exception e)
            {
                return (false, e.Message);
            }
        }

        private string TicketsUrl() => $"{_matchmakerUrl.TrimEnd('/')}/tickets";

        private string TicketUrl(string ticketId) => $"{TicketsUrl()}/{UnityWebRequest.EscapeURL(ticketId)}";

        private async Task<(bool ok, string body, string error)> SendAsync(string method, string url, string jsonBody)
        {
            using var request = new UnityWebRequest(url, method);
            request.downloadHandler = new DownloadHandlerBuffer();

            if (!string.IsNullOrEmpty(jsonBody))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.SetRequestHeader("Content-Type", "application/json");
            }

            if (!string.IsNullOrEmpty(_authToken))
                request.SetRequestHeader("Authorization", _authToken);

            var tcs = new TaskCompletionSource<bool>();
            var operation = request.SendWebRequest();
            operation.completed += _ => tcs.TrySetResult(true);
            await tcs.Task;

            if (request.result != UnityWebRequest.Result.Success)
            {
                var detail = request.downloadHandler?.text;
                var message = string.IsNullOrEmpty(detail) ? request.error : $"{request.error} ({detail})";
                return (false, null, $"HTTP {request.responseCode}: {message}");
            }

            return (true, request.downloadHandler.text, null);
        }

        // --- Edgegap ticket API JSON shapes (https://docs.edgegap.com/learn/matchmaking) ---

        [Serializable, UsedImplicitly]
        private class TicketRequest
        {
            public string profile;
            public Dictionary<string, string> attributes;
            public string player_ip;
        }

        [Serializable]
        private class TicketResponse
        {
            public string id;
            public string status;
            public Assignment assignment;
        }

        [Serializable]
        private class Assignment
        {
            public string fqdn;
            public string public_ip;
            public Dictionary<string, AssignedPort> ports;
        }

        [Serializable]
        private class AssignedPort
        {
            public int external;
            public int @internal;
            public string protocol;
        }
    }
}
