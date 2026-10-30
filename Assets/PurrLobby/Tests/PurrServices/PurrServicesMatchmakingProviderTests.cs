#if PURR_SERVICES_MATCHMAKING
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PurrNet.Lobby.PurrNet;
using PurrNet.Services;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace PurrNet.Lobby.PurrServices.Tests
{
    /// <summary>Controlled clock and identity, with no website, settings asset, or authentication request.</summary>
    public sealed class ControlledServiceMatchmaker : PurrServicesMatchmakingProvider
    {
        public IMatchmakingClient client;
        public string player = "steam:local";
        public string session = "session-one";
        public bool available = true;
        public readonly List<TimeSpan> delays = new();
        readonly Queue<TaskCompletionSource<bool>> _waits = new();
        public MatchmakingTicket? ticket => activeTicket;

        protected override bool isServiceReady => available;
        protected override string localPlayerId => player;
        protected override string localSession => session;
        protected override IMatchmakingClient CaptureClient() => client;
        public override Task Initialize() => Task.CompletedTask;
        protected override Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            delays.Add(delay);
            var source = new TaskCompletionSource<bool>();
            _waits.Enqueue(source);
            var registration = cancellationToken.Register(() => source.TrySetCanceled());
            return ReleaseRegistration(source.Task, registration);
        }

        static async Task ReleaseRegistration(Task task, CancellationTokenRegistration registration)
        {
            try { await task; }
            finally { registration.Dispose(); }
        }

        public void Advance()
        {
            while (_waits.Count > 0)
            {
                var next = _waits.Dequeue();
                if (!next.Task.IsCompleted) { next.SetResult(true); return; }
            }
            Assert.Fail("No pending polling delay to advance");
        }
    }

    public sealed class PurrServicesMatchmakingProviderTests
    {
        sealed class FakeClient : IMatchmakingClient
        {
            public MatchmakingTicketData remote = Ticket("ticket-one", "queued");
            public MatchmakingMatchData match = Match("match-one", "ready");
            public MatchmakingTicketRequest created;
            public string joinedId;
            public string joinedInvite;
            public Dictionary<string, float> joinedLatencies;
            public int createCalls, joinCalls, ticketReads, matchReads, acceptedCalls;
            public readonly List<MatchmakingTicketRequest> createRequests = new();
            public readonly List<CancellationToken> mutationTokens = new();
            public readonly List<string> cancelled = new();
            public readonly List<bool> accepts = new();
            public Func<Task<HttpResult<MatchmakingTicketData>>> create;
            public Func<Task<HttpResult<MatchmakingTicketData>>> join;
            public Func<Task<HttpResult<MatchmakingTicketData>>> readTicket;
            public Func<Task<HttpResult<MatchmakingMatchData>>> readMatch;
            public Func<Task<HttpResult<MatchmakingTicketData>>> cancel;
            public Func<bool, Task<HttpResult<MatchmakingMatchData>>> accept;

            public Task<HttpResult<MatchmakingQueueList>> ListQueuesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Ok(new MatchmakingQueueList()));
            public Task<HttpResult<MatchmakingTicketData>> CreateTicketAsync(MatchmakingTicketRequest request, CancellationToken cancellationToken = default)
            { createCalls++; created = request; createRequests.Add(request); mutationTokens.Add(cancellationToken); return create?.Invoke() ?? Task.FromResult(Ok(remote)); }
            public Task<HttpResult<MatchmakingTicketData>> JoinTicketAsync(string ticketId, string inviteCode, Dictionary<string, float> latencies, CancellationToken cancellationToken = default)
            { joinCalls++; joinedId = ticketId; joinedInvite = inviteCode; joinedLatencies = latencies; mutationTokens.Add(cancellationToken); return join?.Invoke() ?? Task.FromResult(Ok(remote)); }
            public Task<HttpResult<MatchmakingTicketData>> GetTicketAsync(string ticketId, CancellationToken cancellationToken = default)
            { ticketReads++; return readTicket?.Invoke() ?? Task.FromResult(Ok(remote)); }
            public Task<HttpResult<MatchmakingTicketData>> CancelTicketAsync(string ticketId, CancellationToken cancellationToken = default)
            { cancelled.Add(ticketId); return cancel?.Invoke() ?? Task.FromResult(Ok(Ticket(ticketId, "cancelled"))); }
            public Task<HttpResult<MatchmakingMatchData>> GetMatchAsync(string matchId, CancellationToken cancellationToken = default)
            { matchReads++; return readMatch?.Invoke() ?? Task.FromResult(Ok(match)); }
            public Task<HttpResult<MatchmakingMatchData>> AcceptMatchAsync(string matchId, bool value, CancellationToken cancellationToken = default)
            { acceptedCalls++; accepts.Add(value); return accept?.Invoke(value) ?? Task.FromResult(Ok(match)); }
            public Task<HttpResult<MatchmakingMatchData>> PublishConnectionAsync(string matchId, MatchmakingConnection connection, CancellationToken cancellationToken = default) => Task.FromResult(Ok(match));
            public Task<HttpResult<MatchmakingRating>> GetRatingAsync(string queueId, CancellationToken cancellationToken = default) => Task.FromResult(Ok(new MatchmakingRating()));
            public Task<HttpResult<MatchmakingAdmission>> VerifyAdmissionAsync(string matchId, string playerId, string connectionToken, CancellationToken cancellationToken = default) => Task.FromResult(Ok(new MatchmakingAdmission()));
            public Task<HttpResult<MatchmakingResult>> SubmitResultAsync(string matchId, string resultId, int? winningTeam, CancellationToken cancellationToken = default) => Task.FromResult(Ok(new MatchmakingResult()));
        }

        ControlledServiceMatchmaker _provider;
        FakeClient _client;
        readonly List<MatchResult> _found = new();
        readonly List<MatchmakingReadyCheck> _checks = new();
        readonly List<MatchmakingStatus> _statuses = new();
        readonly List<string> _errors = new();

        [SetUp]
        public void SetUp()
        {
            _found.Clear(); _checks.Clear(); _statuses.Clear(); _errors.Clear();
            _client = new FakeClient();
            _provider = ScriptableObject.CreateInstance<ControlledServiceMatchmaker>();
            _provider.client = _client;
            _provider.onMatchFound += (_, result) => _found.Add(result);
            _provider.onReadyCheckChanged += (_, ready) => _checks.Add(ready);
            _provider.onStatusChanged += (_, status) => _statuses.Add(status);
            _provider.onMatchmakingError += (_, error) => _errors.Add(error);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _client.cancel = null;
            if (_provider)
            {
                yield return Await(_provider.Logout());
                UnityEngine.Object.DestroyImmediate(_provider);
            }
        }

        static HttpResult<T> Ok<T>(T value) => new() { success = true, statusCode = 200, data = value };
        static HttpResult<T> Failed<T>(int status, int retry = 0) => new() { success = false, statusCode = status, error = "Expected test failure", retryAfterSeconds = retry };
        static MatchmakingTicketData Ticket(string id, string state, string matchId = null) => new()
        { id = id, state = state, matchId = matchId, leaderId = "steam:local", partySize = 1, players = Array.Empty<MatchmakingPlayer>() };
        static MatchmakingMatchData Match(string id, string state, bool host = false) => new()
        {
            id = id, state = state, hosting = host ? "player-hosted" : "dedicated", ranked = true, isHost = host,
            hostPlayerId = host ? "steam:local" : null, deadline = "2030-01-01T00:00:00Z",
            teams = new[] { new[] { new MatchmakingTeamMember { playerId = "steam:local", accepted = false } }, new[] { new MatchmakingTeamMember { playerId = "steam:other", accepted = false } } },
            connection = state == "active" || (host && state == "allocating")
                ? new MatchmakingConnection { serverAddress = "room", serverPort = 0, connectionToken = "own-private-token", hostId = host ? "steam:local" : null } : null
        };
        static MatchmakingRequest Request() => new() { queueId = "ranked", latencies = new Dictionary<string, int> { ["eu"] = 42 } };

        static IEnumerator Await(Task task)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!task.IsCompleted && timer.Elapsed.TotalSeconds < 5)
            {
                // Batch EditMode can run hundreds of test-enumerator steps before
                // the Unity synchronization context receives its next player-loop
                // update. A frame-count timeout is therefore not an async timeout.
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }
            Assert.IsTrue(task.IsCompleted, "Async provider operation did not settle");
            if (task.IsFaulted) throw task.Exception.GetBaseException();
            Assert.IsFalse(task.IsCanceled, "Unexpected cancellation escaped provider API");
        }

        static IEnumerator Until(Func<bool> condition)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!condition() && timer.Elapsed.TotalSeconds < 5)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }
            Assert.IsTrue(condition(), "Expected provider event/poll did not arrive");
        }

        static async Task NextUnityContinuation() => await Task.Yield();

        [UnityTest]
        public IEnumerator CreateCarriesQueuePartyMeasuredLatencyAndAnIdempotencyKey()
        {
            var request = Request(); request.partySize = 2;
            _client.remote = Ticket("party", "assembling"); _client.remote.inviteCode = "private-invite";
            var start = _provider.StartMatchmaking(request);
            yield return Await(start);
            Assert.IsTrue(start.Result.success);
            Assert.AreEqual("ranked", _client.created.queueId);
            Assert.AreEqual(2, _client.created.partySize);
            Assert.AreEqual(42f, _client.created.latencies["eu"]);
            Assert.IsTrue(Guid.TryParseExact(_client.created.requestId, "N", out _));
            Assert.AreEqual("private-invite", start.Result.ticket.inviteCode);
            yield return Until(() => _provider.delays.Count > 0);
            Assert.Contains(MatchmakingStatus.Assembling, _statuses);
            Assert.IsEmpty(_found);
        }

        [UnityTest]
        public IEnumerator JoiningAnInvitationUsesThisClientWithoutCreatingAnotherParty()
        {
            var request = Request(); request.partyTicketId = "party-id"; request.partyInviteCode = "invite";
            var start = _provider.StartMatchmaking(request);
            yield return Await(start);
            Assert.IsTrue(start.Result.success);
            Assert.AreEqual(0, _client.createCalls); Assert.AreEqual(1, _client.joinCalls);
            Assert.AreEqual("party-id", _client.joinedId); Assert.AreEqual("invite", _client.joinedInvite);
            Assert.AreEqual(42f, _client.joinedLatencies["eu"]);
        }

        [UnityTest]
        public IEnumerator LogoutDuringDeferredCreateCleansUpWithCapturedClientAndNeverHandsOff()
        {
            var pending = new TaskCompletionSource<HttpResult<MatchmakingTicketData>>();
            _client.create = () => pending.Task;
            var start = _provider.StartMatchmaking(Request());
            Assert.IsFalse(start.IsCompleted);
            yield return Await(_provider.Logout());
            var replacementClient = new FakeClient();
            _provider.client = replacementClient; _provider.player = "steam:new"; _provider.session = "new-session";
            pending.SetResult(Ok(Ticket("old-ticket", "queued")));
            yield return Await(start);
            Assert.IsFalse(start.Result.success);
            CollectionAssert.AreEqual(new[] { "old-ticket" }, _client.cancelled);
            Assert.IsEmpty(replacementClient.cancelled);
            Assert.IsEmpty(_found); Assert.IsNull(_provider.ticket);
        }

        [UnityTest]
        public IEnumerator LostCreateResponseRetriesTheSameRequestIdAndBody()
        {
            _client.create = () => Task.FromResult(_client.createCalls == 1 ? Failed<MatchmakingTicketData>(0, 7) : Ok(_client.remote));
            var start = _provider.StartMatchmaking(Request());
            yield return Until(() => _provider.delays.Count == 1);
            Assert.IsFalse(start.IsCompleted); Assert.AreEqual(7, _provider.delays[0].TotalSeconds);
            var original = _client.createRequests[0];
            var requestId = original.requestId;
            _provider.Advance(); yield return Await(start);
            Assert.IsTrue(start.Result.success); Assert.AreEqual(2, _client.createCalls);
            Assert.AreSame(original, _client.createRequests[1]); Assert.AreEqual(requestId, _client.createRequests[1].requestId);
            Assert.AreEqual(42f, _client.createRequests[1].latencies["eu"]);
            Assert.IsFalse(_client.mutationTokens[0].CanBeCanceled); Assert.IsFalse(_client.mutationTokens[1].CanBeCanceled);
        }

        [UnityTest]
        public IEnumerator LogoutWhileRetryWaitsRecoversAndCleansTicketUsingOriginalSession()
        {
            _client.create = () => Task.FromResult(_client.createCalls == 1 ? Failed<MatchmakingTicketData>(0) : Ok(Ticket("recovered", "queued")));
            var start = _provider.StartMatchmaking(Request());
            yield return Until(() => _provider.delays.Count == 1);
            yield return Await(_provider.Logout());
            var next = new FakeClient();
            _provider.client = next; _provider.session = "new-session"; _provider.player = "steam:new";
            _provider.Advance(); yield return Await(start);
            Assert.IsFalse(start.Result.success); Assert.AreEqual(2, _client.createCalls);
            Assert.AreSame(_client.createRequests[0], _client.createRequests[1]);
            CollectionAssert.AreEqual(new[] { "recovered" }, _client.cancelled);
            Assert.AreEqual(0, next.createCalls); Assert.IsEmpty(next.cancelled);
            Assert.IsNull(_provider.ticket); Assert.IsEmpty(_found);
        }

        [UnityTest]
        public IEnumerator LostJoinResponseRetriesTheSameInvitation()
        {
            _client.join = () => Task.FromResult(_client.joinCalls == 1 ? Failed<MatchmakingTicketData>(408) : Ok(_client.remote));
            var request = Request(); request.partyTicketId = "party"; request.partyInviteCode = "private-invite";
            var start = _provider.StartMatchmaking(request);
            yield return Until(() => _provider.delays.Count == 1);
            var latencies = _client.joinedLatencies;
            _provider.Advance(); yield return Await(start);
            Assert.IsTrue(start.Result.success); Assert.AreEqual(0, _client.createCalls); Assert.AreEqual(2, _client.joinCalls);
            Assert.AreEqual("party", _client.joinedId); Assert.AreEqual("private-invite", _client.joinedInvite);
            Assert.AreSame(latencies, _client.joinedLatencies);
        }

        [UnityTest]
        public IEnumerator LoggedOutCreateRecoveryMakesOnlyOneFurtherAttempt()
        {
            _client.create = () => Task.FromResult(Failed<MatchmakingTicketData>(0));
            var start = _provider.StartMatchmaking(Request());
            yield return Until(() => _provider.delays.Count == 1);
            yield return Await(_provider.Logout());
            _provider.Advance(); yield return Await(start);
            Assert.IsFalse(start.Result.success); Assert.AreEqual(2, _client.createCalls);
            Assert.AreEqual(1, _provider.delays.Count); Assert.IsNull(_provider.ticket); Assert.IsEmpty(_found);
        }

        [UnityTest]
        public IEnumerator UnavailableTicketServiceStopsAfterTwoRetries()
        {
            _client.create = () => Task.FromResult(Failed<MatchmakingTicketData>(503));
            var start = _provider.StartMatchmaking(Request());
            yield return Until(() => _provider.delays.Count == 1);
            _provider.Advance(); yield return Until(() => _provider.delays.Count == 2);
            _provider.Advance(); yield return Await(start);
            Assert.IsFalse(start.Result.success); Assert.AreEqual(3, _client.createCalls);
            Assert.AreSame(_client.createRequests[0], _client.createRequests[2]);
            Assert.AreEqual(2, _provider.delays.Count); Assert.IsNull(_provider.ticket);
        }

        [UnityTest]
        public IEnumerator TicketCreationDoesNotRetryClientErrorsOrExcessiveRetryAfter()
        {
            foreach (int status in new[] { 400, 401, 409, 429, 503 })
            {
                _client.create = () => Task.FromResult(Failed<MatchmakingTicketData>(status, status == 503 ? 31 : 0));
                int before = _client.createCalls;
                var start = _provider.StartMatchmaking(Request());
                yield return Await(start);
                Assert.IsFalse(start.Result.success); Assert.AreEqual(before + 1, _client.createCalls);
                Assert.IsEmpty(_provider.delays); Assert.IsNull(_provider.ticket);
            }
        }

        [UnityTest]
        public IEnumerator ReadyChecksNeverAutoAcceptAndRequireAnExplicitResponse()
        {
            _client.remote = Ticket("ticket", "matched", "match-one");
            var start = _provider.StartMatchmaking(Request());
            yield return Await(start); yield return Until(() => _checks.Count == 1);
            Assert.AreEqual(0, _client.acceptedCalls); Assert.IsEmpty(_found);
            Assert.AreEqual(2, _checks[0].totalPlayers); Assert.IsFalse(_checks[0].accepted);
            var response = _provider.RespondToReadyCheck(start.Result.ticket, true);
            yield return Await(response);
            Assert.IsTrue(response.Result.success); CollectionAssert.AreEqual(new[] { true }, _client.accepts);
            Assert.IsEmpty(_found);
        }

        [UnityTest]
        public IEnumerator FailedReadyCheckCanRequeueTheSameTicketAndOfferANewMatch()
        {
            _client.remote = Ticket("ticket", "matched", "old-match"); _client.match = Match("old-match", "expired");
            var start = _provider.StartMatchmaking(Request());
            yield return Await(start); yield return Until(() => _provider.delays.Count == 1);
            Assert.IsEmpty(_errors); Assert.IsEmpty(_found);
            _client.remote = Ticket("ticket", "queued");
            _provider.Advance(); yield return Until(() => _provider.delays.Count == 2);
            Assert.AreEqual("ticket", _provider.ticket?.ticketId);
            Assert.IsNull(_provider.currentMatch);
            _client.remote = Ticket("ticket", "matched", "new-match"); _client.match = Match("new-match", "ready");
            _provider.Advance(); yield return Until(() => _checks.Count == 1);
            Assert.AreEqual("new-match", _checks[0].matchId); Assert.IsEmpty(_errors);
        }

        [UnityTest]
        public IEnumerator DedicatedAllocationWaitsUntilActiveThenHandsOffExactlyOnce()
        {
            _client.remote = Ticket("ticket", "matched", "match-one"); _client.match = Match("match-one", "allocating");
            var start = _provider.StartMatchmaking(Request());
            yield return Await(start); yield return Until(() => _provider.delays.Count == 1);
            Assert.IsEmpty(_found);
            _client.match = Match("match-one", "active");
            _provider.Advance(); yield return Until(() => _found.Count == 1);
            Assert.IsFalse(_found[0].isHost); Assert.IsFalse(_found[0].requiresHostReady);
            Assert.AreEqual("own-private-token", _found[0].connection.connectionToken);
            Assert.IsNull(_provider.ticket);
            yield return Await(NextUnityContinuation());
            Assert.AreEqual(1, _found.Count);
        }

        [UnityTest]
        public IEnumerator ElectedHostHandoffRequiresListenerAcknowledgmentBeforePeersJoin()
        {
            _client.remote = Ticket("ticket", "matched", "match-one"); _client.match = Match("match-one", "allocating", true);
            var start = _provider.StartMatchmaking(Request());
            yield return Await(start); yield return Until(() => _found.Count == 1);
            Assert.IsTrue(_found[0].isHost); Assert.IsTrue(_found[0].requiresHostReady);
            Assert.AreEqual("match-one", _found[0].matchId);
            Assert.AreEqual("steam:local", _found[0].playerId);
        }

        [UnityTest]
        public IEnumerator FailedHostStartupCancelsTheHandedOffTicketWithItsCapturedClient()
        {
            _client.remote = Ticket("host-ticket", "matched", "host-match"); _client.match = Match("host-match", "allocating", true);
            var start = _provider.StartMatchmaking(Request());
            yield return Await(start); yield return Until(() => _found.Count == 1);
            Assert.IsNull(_provider.ticket);
            var next = new FakeClient();
            _provider.client = next; _provider.session = "new-session";
            var cancel = _provider.CancelMatchmaking(start.Result.ticket);
            yield return Await(cancel);
            Assert.IsTrue(cancel.Result.success);
            CollectionAssert.AreEqual(new[] { "host-ticket" }, _client.cancelled);
            Assert.IsEmpty(next.cancelled);
            var duplicate = _provider.CancelMatchmaking(start.Result.ticket);
            yield return Await(duplicate);
            Assert.IsFalse(duplicate.Result.success); Assert.AreEqual(1, _client.cancelled.Count);
        }

        [UnityTest]
        public IEnumerator DeferredHostCleanupCannotCancelOrClearANewerSearch()
        {
            _client.remote = Ticket("host-ticket", "matched", "host-match"); _client.match = Match("host-match", "allocating", true);
            var first = _provider.StartMatchmaking(Request());
            yield return Await(first); yield return Until(() => _found.Count == 1);
            var pending = new TaskCompletionSource<HttpResult<MatchmakingTicketData>>();
            _client.cancel = () => pending.Task;
            var cleanup = _provider.CancelMatchmaking(first.Result.ticket);
            Assert.IsFalse(cleanup.IsCompleted);

            var next = new FakeClient { remote = Ticket("new-ticket", "queued") };
            _provider.client = next; _provider.session = "new-session";
            var second = _provider.StartMatchmaking(Request());
            yield return Await(second);
            Assert.IsTrue(second.Result.success);
            pending.SetResult(Ok(Ticket("host-ticket", "cancelled")));
            yield return Await(cleanup);
            Assert.IsTrue(cleanup.Result.success);
            Assert.AreEqual("new-ticket", _provider.ticket?.ticketId); Assert.IsEmpty(next.cancelled);
            var stale = _provider.CancelMatchmaking(first.Result.ticket);
            yield return Await(stale);
            Assert.IsFalse(stale.Result.success);
            Assert.AreEqual("new-ticket", _provider.ticket?.ticketId); Assert.IsEmpty(next.cancelled);
        }

        [UnityTest]
        public IEnumerator LogoutBeforeHostPublicationCleansTheCapturedAllocationOnce()
        {
            _client.remote = Ticket("host-ticket", "matched", "host-match"); _client.match = Match("host-match", "allocating", true);
            var start = _provider.StartMatchmaking(Request());
            yield return Await(start); yield return Until(() => _found.Count == 1);
            var next = new FakeClient();
            _provider.client = next; _provider.session = "new-session";
            yield return Await(_provider.Logout());
            CollectionAssert.AreEqual(new[] { "host-ticket" }, _client.cancelled);
            Assert.IsEmpty(next.cancelled); Assert.IsNull(_provider.ticket); Assert.IsNull(_provider.currentMatch);
            yield return Await(_provider.Logout());
            Assert.AreEqual(1, _client.cancelled.Count); Assert.AreEqual(1, _found.Count);
        }

        [UnityTest]
        public IEnumerator CancellingFromAllocationStatusCannotHandOffWhileDeleteIsPending()
        {
            _client.remote = Ticket("host-ticket", "matched", "host-match"); _client.match = Match("host-match", "allocating", true);
            var pending = new TaskCompletionSource<HttpResult<MatchmakingTicketData>>();
            _client.cancel = () => pending.Task;
            Task<APIResponse> cancellation = null;
            _provider.onStatusChanged += (ticket, status) =>
            {
                if (status == MatchmakingStatus.Allocating) cancellation = _provider.CancelMatchmaking(ticket);
            };
            var start = _provider.StartMatchmaking(Request());
            yield return Await(start); yield return Until(() => cancellation != null);
            yield return Await(NextUnityContinuation());
            Assert.IsEmpty(_found, "A status callback cancelled before the host listener started");
            pending.SetResult(Ok(Ticket("host-ticket", "cancelled")));
            yield return Await(cancellation);
            Assert.IsTrue(cancellation.Result.success); Assert.IsNull(_provider.ticket);
        }

        [UnityTest]
        public IEnumerator FailedCancellationResumesPollingAndAllowsAnotherCancellation()
        {
            var start = _provider.StartMatchmaking(Request());
            yield return Await(start); yield return Until(() => _provider.delays.Count == 1);
            _client.cancel = () => Task.FromResult(Failed<MatchmakingTicketData>(503));
            var cancel = _provider.CancelMatchmaking(start.Result.ticket);
            yield return Await(cancel);
            Assert.IsFalse(cancel.Result.success); Assert.IsNotNull(_provider.ticket);
            yield return Until(() => _provider.delays.Count == 2);
            Assert.GreaterOrEqual(_client.ticketReads, 2);
            _client.cancel = null;
            var retry = _provider.CancelMatchmaking(start.Result.ticket);
            yield return Await(retry);
            Assert.IsTrue(retry.Result.success); Assert.IsNull(_provider.ticket);
            Assert.AreEqual(2, _client.cancelled.Count);
        }

        [UnityTest]
        public IEnumerator RetryAfterOn429DelaysTheNextPollWithoutLosingTheTicket()
        {
            _client.readTicket = () => Task.FromResult(Failed<MatchmakingTicketData>(429, 17));
            var start = _provider.StartMatchmaking(Request());
            yield return Await(start); yield return Until(() => _provider.delays.Count == 1);
            Assert.GreaterOrEqual(_provider.delays[0].TotalSeconds, 17);
            Assert.IsNotNull(_provider.ticket); Assert.IsEmpty(_errors);
            _client.readTicket = null;
            _provider.Advance(); yield return Until(() => _provider.delays.Count == 2);
            Assert.AreEqual(2, _client.ticketReads); Assert.IsEmpty(_found);
        }

        [UnityTest]
        public IEnumerator CancellingWhileAMatchResponseIsDelayedCannotStartGameplay()
        {
            var matchResponse = new TaskCompletionSource<HttpResult<MatchmakingMatchData>>();
            var cancellation = new TaskCompletionSource<HttpResult<MatchmakingTicketData>>();
            _client.remote = Ticket("ticket", "matched", "match-one");
            _client.readMatch = () => matchResponse.Task;
            _client.cancel = () => cancellation.Task;
            var start = _provider.StartMatchmaking(Request());
            yield return Await(start); yield return Until(() => _client.matchReads == 1);
            var cancel = _provider.CancelMatchmaking(start.Result.ticket);
            matchResponse.SetResult(Ok(Match("match-one", "active")));
            yield return Await(NextUnityContinuation());
            Assert.IsEmpty(_found, "An in-flight poll must not hand off after cancellation begins");
            cancellation.SetResult(Ok(Ticket("ticket", "cancelled")));
            yield return Await(cancel);
            Assert.IsTrue(cancel.Result.success); Assert.IsNull(_provider.ticket);
        }

        [UnityTest]
        public IEnumerator OldReadyResponseCannotAffectNewSearchOrClearItsResponseGuard()
        {
            var oldResponse = new TaskCompletionSource<HttpResult<MatchmakingMatchData>>();
            _client.remote = Ticket("old-ticket", "matched", "old-match"); _client.match = Match("old-match", "ready");
            _client.accept = _ => oldResponse.Task;
            var first = _provider.StartMatchmaking(Request());
            yield return Await(first); yield return Until(() => _checks.Count == 1);
            var oldAccept = _provider.RespondToReadyCheck(first.Result.ticket, true);
            yield return Await(_provider.Logout());

            var next = new FakeClient { remote = Ticket("new-ticket", "matched", "new-match"), match = Match("new-match", "ready") };
            var nextResponse = new TaskCompletionSource<HttpResult<MatchmakingMatchData>>();
            next.accept = _ => nextResponse.Task;
            _provider.client = next; _provider.session = "new-session";
            var second = _provider.StartMatchmaking(Request());
            yield return Await(second); yield return Until(() => _checks.Count == 2);
            var newAccept = _provider.RespondToReadyCheck(second.Result.ticket, true);
            Assert.AreEqual(1, next.acceptedCalls, "An old ready response must not block the new search");
            oldResponse.SetResult(Ok(Match("old-match", "ready")));
            yield return Await(oldAccept);
            Assert.IsFalse(oldAccept.Result.success);
            var duplicate = _provider.RespondToReadyCheck(second.Result.ticket, true);
            yield return Await(duplicate);
            Assert.IsFalse(duplicate.Result.success); Assert.AreEqual(1, next.acceptedCalls);
            Assert.AreEqual("new-ticket", _provider.ticket?.ticketId); Assert.IsEmpty(_found);
            nextResponse.SetResult(Ok(Match("new-match", "ready")));
            yield return Await(newAccept);
        }
    }
}
#endif
