using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
#if PURR_SERVICES_MATCHMAKING
using PurrNet.Services;
#endif

namespace PurrNet.Lobby.PurrNet
{
    /// <summary>Durable service queues, explicit ready checks, and party invitations.</summary>
    [ProviderDependency("dev.purrnet.services", "PurrServices")]
    [CreateAssetMenu(menuName = "PurrLobby/PurrNet/Service Matchmaker", order = -199)]
    public class PurrServicesMatchmakingProvider : MatchmakingProvider
    {
        [Tooltip("Queue ID configured on the project's Matchmaking dashboard. A request.queueId overrides this.")]
        [SerializeField] private string _queueId = "casual";
        [SerializeField, Range(3, 10)] private float _pollInterval = 3f;
        [Tooltip("Regional HTTP ping endpoints, used when the request supplies no measured latencies. Never substitutes a guessed latency.")]
        [SerializeField] private MatchmakingRegionProbe[] _latencyProbes = Array.Empty<MatchmakingRegionProbe>();

#if PURR_SERVICES_MATCHMAKING
        private IMatchmakingClient _client;
        private AuthService _auth;
        private CancellationTokenSource _pollCancellation;
        private int _generation;
        private bool _starting;
        private Task<APIResponse> _cancelTask;
        private string _session;
        private string _playerId;
        private string _readyMatchId;
        private bool _responding;
        private MatchmakingTicketData _remoteTicket;
        private MatchmakingMatchData _currentMatch;
        private MatchmakingTicket? _handedOffTicket;
        private IMatchmakingClient _handoffClient;

        /// <summary>The last match snapshot. Its connection credentials belong only to this player.</summary>
        public MatchmakingMatchData currentMatch => _currentMatch;
        public MatchmakingTicketData currentParty => _remoteTicket;
        public event Action<MatchmakingTicketData> onPartyChanged;

        // Virtual boundaries allow lifecycle tests without live sessions or networking.
        protected virtual bool isServiceReady => PurrServices.instance.isConfigured && PurrServices.instance.isAuthenticated;
        protected virtual string localPlayerId => PurrServices.instance.playerId;
        protected virtual string localSession => PurrServices.instance.sessionToken;
        protected virtual IMatchmakingClient CaptureClient() => PurrServices.instance.matchmaking.CaptureSession();
        protected virtual Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.Delay(delay, cancellationToken);

        public override Task Initialize()
        {
            UnsubscribeAuth();
            _auth = PurrServices.instance.auth;
            _auth.onLoggedIn += OnSessionChanged;
            _auth.onLoggedOut += OnSessionChanged;
            return Task.CompletedTask;
        }

        private void OnSessionChanged()
        {
            if (_session != null && _auth?.sessionToken != _session)
                Logout().Forget("[Matchmaking] Session cleanup failed");
        }

        private void UnsubscribeAuth()
        {
            if (_auth == null) return;
            _auth.onLoggedIn -= OnSessionChanged;
            _auth.onLoggedOut -= OnSessionChanged;
            _auth = null;
        }

        private void OnDisable()
        {
            UnsubscribeAuth();
            Logout().Forget("[Matchmaking] Provider cleanup failed");
        }

        public override async Task<MatchmakingTicketResponse> StartMatchmaking(MatchmakingRequest request)
        {
            if (_starting || activeTicket.HasValue || _cancelTask != null)
                return MatchmakingTicketResponse.Failure("A matchmaking search is already running.");
            if (!isServiceReady || string.IsNullOrEmpty(localPlayerId))
                return MatchmakingTicketResponse.Failure("Sign in to a configured PurrServices project first.");

            _starting = true;
            int generation = ++_generation;
            var client = CaptureClient();
            _client = client;
            _session = localSession;
            _playerId = localPlayerId;
            _currentMatch = null;
            _handedOffTicket = null;
            _handoffClient = null;
            _remoteTicket = null;
            _readyMatchId = null;
            _responding = false;
            _pollCancellation = new CancellationTokenSource();
            var cancellation = _pollCancellation.Token;
            try
            {
                var measurements = await CollectLatenciesAsync(request, cancellation);
                cancellation.ThrowIfCancellationRequested();
                if (measurements == null || measurements.Count == 0)
                    return MatchmakingTicketResponse.Failure("No region measurements are available. Configure reachable latency probes or supply measured request.latencies.");
                // Keep the create/join request alive when the UI closes. Its
                // response supplies the ID needed to clean up a server-side
                // mutation, using the original captured session, not a new login.
                HttpResult<MatchmakingTicketData> response;
                if (!string.IsNullOrEmpty(request.partyTicketId))
                {
                    if (string.IsNullOrEmpty(request.partyInviteCode))
                        return MatchmakingTicketResponse.Failure("A party invitation code is required.");
                    response = await MutateTicketAsync(
                        () => client.JoinTicketAsync(request.partyTicketId, request.partyInviteCode, measurements), generation);
                }
                else
                {
                    var createRequest = new MatchmakingTicketRequest
                    {
                        queueId = string.IsNullOrEmpty(request.queueId) ? _queueId : request.queueId,
                        partySize = request.partySize == 0 ? 1 : request.partySize,
                        requestId = Guid.NewGuid().ToString("N"),
                        latencies = measurements
                    };
                    response = await MutateTicketAsync(() => client.CreateTicketAsync(createRequest), generation);
                }
                if (generation != _generation)
                {
                    if (response.success && response.data != null) await CleanupTicket(client, response.data.id);
                    return MatchmakingTicketResponse.Failure("Matchmaking was cancelled.");
                }
                if (!response.success || response.data == null || string.IsNullOrEmpty(response.data.id))
                    return MatchmakingTicketResponse.Failure(response.error ?? "No matchmaking ticket was returned.");

                _remoteTicket = response.data;
                var ticket = BeginTicket(response.data.id, response.data.inviteCode);
                onPartyChanged?.Invoke(response.data);
                PollAsync(ticket, client, generation, cancellation).Forget("[Matchmaking] Polling failed");
                return MatchmakingTicketResponse.Success(ticket);
            }
            catch (OperationCanceledException) { return MatchmakingTicketResponse.Failure("Matchmaking was cancelled."); }
            catch (Exception)
            {
                // Transport internals and credentials must not reach logs/toasts.
                return MatchmakingTicketResponse.Failure("Unable to start matchmaking. Please try again.");
            }
            finally
            {
                _starting = false;
                if (generation == _generation && !activeTicket.HasValue) StopPolling();
            }
        }

        private async Task<HttpResult<MatchmakingTicketData>> MutateTicketAsync(
            Func<Task<HttpResult<MatchmakingTicketData>>> mutation, int generation)
        {
            HttpResult<MatchmakingTicketData> response = default;
            int recoveryAttempts = 0;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                // After logout, make at most one further idempotent attempt to
                // recover the unknown ticket ID and let Start clean it up. The
                // same captured client and request ID are used for every attempt.
                if (attempt > 0 && generation != _generation && ++recoveryAttempts > 1)
                    return response;
                response = await mutation();
                bool retryable = response.statusCode == 0 || response.statusCode == 408 ||
                    (response.statusCode >= 500 && response.statusCode < 600);
                if (response.success || !retryable || attempt == 2 || response.retryAfterSeconds > 30 ||
                    (generation != _generation && recoveryAttempts > 0))
                    return response;

                // This delay is deliberately separate from polling cancellation:
                // cancelling a committed mutation before its ID is known would
                // prevent cleanup. Each request has the SDK's finite timeout.
                await WaitAsync(TimeSpan.FromSeconds(Math.Max(response.retryAfterSeconds, attempt + 1)), CancellationToken.None);
            }
            return response;
        }

        protected virtual async Task<Dictionary<string, float>> CollectLatenciesAsync(MatchmakingRequest request, CancellationToken cancellationToken)
        {
            if (request.latencies != null && request.latencies.Count > 0)
            {
                var supplied = new Dictionary<string, float>();
                foreach (var pair in request.latencies) supplied.Add(pair.Key, pair.Value);
                return supplied;
            }
            return await MatchmakingRegionProbe.MeasureAsync(_latencyProbes, cancellationToken);
        }

        private async Task PollAsync(MatchmakingTicket ticket, IMatchmakingClient client, int generation, CancellationToken cancellationToken)
        {
            await Task.Yield();
            int failures = 0;
            int terminalWaits = 0;
            try
            {
                while (generation == _generation && !IsStale(ticket))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var response = await client.GetTicketAsync(ticket.ticketId, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (generation != _generation || IsStale(ticket)) return;
                    if (!response.success || response.data == null)
                    {
                        if (Transient(response.statusCode) && ++failures <= 8)
                        { await Backoff(response.retryAfterSeconds, failures, cancellationToken); continue; }
                        await EndFailed(client, ticket, response.error ?? "Unable to read matchmaking ticket."); return;
                    }
                    _remoteTicket = response.data;
                    onPartyChanged?.Invoke(response.data);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (response.data.state == "assembling" || response.data.state == "queued")
                    {
                        failures = 0; terminalWaits = 0; _readyMatchId = null; _currentMatch = null;
                        RaiseStatusChanged(ticket, response.data.state == "assembling" ? MatchmakingStatus.Assembling : MatchmakingStatus.Searching);
                    }
                    else if (response.data.state == "matched" && !string.IsNullOrEmpty(response.data.matchId))
                    {
                        var matched = await client.GetMatchAsync(response.data.matchId, cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (generation != _generation || IsStale(ticket)) return;
                        if (!matched.success || matched.data == null)
                        {
                            if (Transient(matched.statusCode) && ++failures <= 8)
                            { await Backoff(matched.retryAfterSeconds, failures, cancellationToken); continue; }
                            await EndFailed(client, ticket, matched.error ?? "Unable to read match."); return;
                        }
                        failures = 0;
                        var match = _currentMatch = matched.data;
                        if (match.state == "ready")
                        {
                            terminalWaits = 0;
                            _readyMatchId = match.id;
                            RaiseStatusChanged(ticket, MatchmakingStatus.ReadyCheck);
                            cancellationToken.ThrowIfCancellationRequested();
                            NotifyReadyCheck(ticket, match);
                        }
                        else if (match.state == "allocating")
                        {
                            terminalWaits = 0; _readyMatchId = null;
                            RaiseStatusChanged(ticket, MatchmakingStatus.Allocating);
                            cancellationToken.ThrowIfCancellationRequested();
                            // The elected host must enter the game to start its
                            // listener before it can acknowledge allocation.
                            if (match.isHost && match.hosting == "player-hosted" && match.connection != null)
                            { Handoff(ticket, match, true); return; }
                        }
                        else if (match.state == "active" && match.connection != null)
                        { Handoff(ticket, match, false); return; }
                        else if (match.state == "cancelled" || match.state == "expired")
                        {
                            _readyMatchId = null;
                            RaiseStatusChanged(ticket, MatchmakingStatus.Searching);
                            // Expiry reads can precede the worker's requeue commit.
                            // Keep polling the ticket to recover its preserved age.
                            if (++terminalWaits > 10)
                            { await EndFailed(client, ticket, "The match ended before the game could start."); return; }
                        }
                        else { await EndFailed(client, ticket, "The match is no longer available."); return; }
                    }
                    else
                    {
                        StopPolling();
                        if (response.data.state == "cancelled") CancelLocally(ticket);
                        else FailMatch(ticket, "Your matchmaking ticket expired or ended. Start a new search.");
                        return;
                    }
                    await WaitAsync(TimeSpan.FromSeconds(Math.Max(3, _pollInterval)), cancellationToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                if (generation == _generation && !IsStale(ticket))
                    await EndFailed(client, ticket, "Matchmaking connection failed. Please try again.");
            }
        }

        private static bool Transient(int status) => status == 0 || status == 429 || status >= 500;
        private Task Backoff(int retryAfterSeconds, int failures, CancellationToken cancellationToken) =>
            WaitAsync(TimeSpan.FromSeconds(Math.Max(retryAfterSeconds, Math.Min(30, Math.Max(3, _pollInterval) * Math.Pow(2, failures - 1)))), cancellationToken);

        private void NotifyReadyCheck(MatchmakingTicket ticket, MatchmakingMatchData match)
        {
            int total = 0, accepted = 0;
            bool ownAccepted = false;
            foreach (var team in match.teams ?? Array.Empty<MatchmakingTeamMember[]>())
                foreach (var player in team ?? Array.Empty<MatchmakingTeamMember>())
                {
                    total++;
                    if (player.accepted) accepted++;
                    if (player.playerId == _playerId) ownAccepted = player.accepted;
                }
            RaiseReadyCheck(ticket, new MatchmakingReadyCheck
            { matchId = match.id, deadline = match.deadline, totalPlayers = total, acceptedPlayers = accepted, accepted = ownAccepted });
        }

        private void Handoff(MatchmakingTicket ticket, MatchmakingMatchData match, bool hostReady)
        {
            if (string.IsNullOrEmpty(match.connection.serverAddress) || string.IsNullOrEmpty(match.connection.connectionToken))
                throw new InvalidOperationException("The match has no authenticated connection.");
            StopPolling();
            _readyMatchId = null;
            if (hostReady) { _handedOffTicket = ticket; _handoffClient = _client; }
            CompleteMatch(ticket, new MatchResult
            {
                matchId = match.id, playerId = _playerId, region = match.region, ranked = match.ranked,
                isHost = match.isHost, requiresHostReady = hostReady,
                connection = new ConnectionInfo
                {
                    serverAddress = match.connection.serverAddress, serverPort = match.connection.serverPort,
                    connectionToken = match.connection.connectionToken, hostId = match.connection.hostId
                }
            });
        }

        public override async Task<APIResponse> RespondToReadyCheck(MatchmakingTicket ticket, bool accept)
        {
            if (IsStale(ticket) || string.IsNullOrEmpty(_readyMatchId)) return APIResponse.Failure("There is no active ready check.");
            if (_responding) return APIResponse.Failure("Your response is already being sent.");
            _responding = true;
            int generation = _generation;
            string matchId = _readyMatchId;
            try
            {
                var response = await _client.AcceptMatchAsync(matchId, accept);
                if (generation != _generation || IsStale(ticket)) return APIResponse.Failure("The ready check has ended.");
                if (!response.success) return APIResponse.Failure(response.error);
                if (!accept) { StopPolling(); CancelLocally(ticket); }
                else if (response.data?.state == "ready") NotifyReadyCheck(ticket, response.data);
                return APIResponse.Success();
            }
            catch (Exception) { return APIResponse.Failure("Unable to send your ready-check response."); }
            finally { if (generation == _generation) _responding = false; }
        }

        public override async Task<APIResponse> CancelMatchmaking(MatchmakingTicket ticket)
        {
            // Host scene loading can fail after CompleteMatch consumes the local
            // ticket but before the listener is published. Keep its captured
            // identity for that cleanup; the server rejects active-game aborts.
            if (!activeTicket.HasValue && _handedOffTicket?.ticketId == ticket.ticketId)
            {
                var client = _handoffClient;
                int generation = _generation;
                try
                {
                    var response = await client.CancelTicketAsync(ticket.ticketId);
                    if (!response.success) return APIResponse.Failure(response.error);
                    if (generation == _generation) { _handedOffTicket = null; _handoffClient = null; }
                    return APIResponse.Success();
                }
                catch (Exception) { return APIResponse.Failure("Unable to cancel the pending game allocation."); }
            }
            if (_cancelTask != null) return await _cancelTask;
            if (IsStale(ticket)) return APIResponse.Failure("No active matchmaking with that ticket.");
            var pending = CancelAsync(ticket);
            _cancelTask = pending;
            try { return await pending; }
            finally { if (_cancelTask == pending) _cancelTask = null; }
        }

        private async Task<APIResponse> CancelAsync(MatchmakingTicket ticket)
        {
            var client = _client;
            int generation = _generation;
            StopPolling();
            try
            {
                var response = await client.CancelTicketAsync(ticket.ticketId);
                if (generation != _generation || IsStale(ticket)) return APIResponse.Success();
                if (!response.success)
                {
                    _pollCancellation = new CancellationTokenSource();
                    PollAsync(ticket, client, generation, _pollCancellation.Token).Forget("[Matchmaking] Polling failed");
                    return APIResponse.Failure(response.error);
                }
                ++_generation;
                CancelLocally(ticket);
                return APIResponse.Success();
            }
            catch (Exception)
            {
                if (generation == _generation && !IsStale(ticket))
                {
                    _pollCancellation = new CancellationTokenSource();
                    PollAsync(ticket, client, generation, _pollCancellation.Token).Forget("[Matchmaking] Polling failed");
                }
                return APIResponse.Failure("Unable to cancel. Please try again.");
            }
        }

        private async Task EndFailed(IMatchmakingClient client, MatchmakingTicket ticket, string message)
        {
            StopPolling();
            // Clear/notify before awaiting cleanup, so a later response cannot
            // clear a new ticket created in an event handler.
            if (!IsStale(ticket)) FailMatch(ticket, message);
            await CleanupTicket(client, ticket.ticketId);
        }

        private static async Task CleanupTicket(IMatchmakingClient client, string ticketId)
        {
            if (client == null || string.IsNullOrEmpty(ticketId)) return;
            try { await client.CancelTicketAsync(ticketId); }
            catch (Exception) { /* Dead sessions fall back to server-side deadlines. */ }
        }

        private void StopPolling()
        {
            _pollCancellation?.Cancel();
            _pollCancellation?.Dispose();
            _pollCancellation = null;
        }

        public override async Task Logout()
        {
            ++_generation;
            StopPolling();
            var client = _client;
            var ticket = activeTicket;
            var handoff = _handedOffTicket;
            var handoffClient = _handoffClient;
            if (ticket.HasValue) CancelLocally(ticket.Value);
            _client = null; _session = null; _readyMatchId = null;
            _responding = false;
            _remoteTicket = null; _currentMatch = null;
            _handedOffTicket = null; _handoffClient = null;
            if (ticket.HasValue) await CleanupTicket(client, ticket.Value.ticketId);
            else if (handoff.HasValue) await CleanupTicket(handoffClient, handoff.Value.ticketId);
        }
#else
        public override Task<MatchmakingTicketResponse> StartMatchmaking(MatchmakingRequest request) =>
            Task.FromResult(MatchmakingTicketResponse.Failure("Service matchmaking requires PurrServices 1.3.0 or newer."));
        public override Task<APIResponse> CancelMatchmaking(MatchmakingTicket ticket) =>
            Task.FromResult(APIResponse.Failure("Service matchmaking is not installed."));
#endif
    }
}
