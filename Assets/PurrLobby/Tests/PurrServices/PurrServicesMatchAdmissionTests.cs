#if PURR_SERVICES_MATCHMAKING
using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PurrNet.Authentication;
using PurrNet.Lobby.PurrNet;
using PurrNet.Packing;
using PurrNet.Services;
using PurrNet.Transports;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PurrNet.Lobby.Tests
{
    public class TestMatchAuthenticator : PurrServicesMatchAuthenticator
    {
        public Task<AuthenticationResponse> Validate(Connection connection, MatchAuthPayload payload) =>
            ValidateClientPayload(connection, payload);
        public Task<AuthenticationRequest<MatchAuthPayload>> ClientPayload() => GetClientPayload();
        public void Disconnect(Connection connection) => UnAuthenticateClient(connection);
    }

    public class PurrServicesMatchAdmissionTests
    {
        const string MatchId = "92db3997-e783-43f4-b6db-d691a8fa51d7";
        const string PlayerId = "player-a";
        const string Token = "test-admission-credential";
        GameObject _object;
        NetworkManager _manager;
        TestMatchAuthenticator _admission;

        [SetUp]
        public void SetUp()
        {
            // No listeners, native sockets, or HTTP are started by these tests.
            _object = new GameObject("Match admission tests");
            _object.SetActive(false);
            _manager = _object.AddComponent<NetworkManager>();
            _manager.transport = _object.AddComponent<UDPTransport>();
            _admission = _object.AddComponent<TestMatchAuthenticator>();
        }

        [TearDown]
        public void TearDown()
        {
            _admission.ResetContext();
            Object.DestroyImmediate(_object);
        }

        static MatchAuthPayload Payload(string playerId = PlayerId) => new()
        {
            matchId = MatchId, playerId = playerId, connectionToken = Token
        };

        static HttpResult<MatchmakingAdmission> Accepted(string playerId = PlayerId, int? team = 1) => new()
        {
            success = true, statusCode = 200,
            data = new MatchmakingAdmission { valid = true, playerId = playerId, team = team }
        };

        void Setup(PurrServicesMatchAuthenticator.VerifyAdmission verify) =>
            _admission.Setup(_manager, MatchId, PlayerId, Token, verify);

        [Test]
        public void AdmissionPayload_RoundTripsThroughGeneratedPurrNetSerializer()
        {
            // These fixtures deliberately keep NetworkManager inactive. Run
            // the same registration entry point used by its runtime Awake.
            NetworkManager.LoadOrGenerateHashes();
            Assert.IsTrue(Packer<MatchAuthPayload>.HasPacker(),
                "The payload must use generated serializers, never reflection fallback.");
            Assert.IsTrue(Packer<MatchAuthPayload>.DirectWrite.Method.DeclaringType
                .IsDefined(typeof(GeneratedByILAttribute), false));
            Assert.AreSame(typeof(MatchAuthPayload).Assembly,
                Packer<MatchAuthPayload>.DirectWrite.Method.DeclaringType.Assembly);
            // AuthenticationBehaviour sends the version followed by T itself;
            // AuthenticationRequest<T> is not the serialized wire envelope.
            using var writer = BitPackerPool.Get();
            Packer<string>.Write(writer, NetworkManager.version);
            Packer<MatchAuthPayload>.Write(writer, Payload());
            using var reader = BitPackerPool.Get(writer.ToByteData());
            string version = null;
            MatchAuthPayload payload = default;
            Packer<string>.Read(reader, ref version);
            Packer<MatchAuthPayload>.Read(reader, ref payload);
            Assert.AreEqual(NetworkManager.version, version);
            Assert.AreEqual(MatchId, payload.matchId);
            Assert.AreEqual(PlayerId, payload.playerId);
            Assert.AreEqual(Token, payload.connectionToken);
        }

        [Test]
        public async Task ClientSetup_InstallsAuthenticatorAndSendsAssignedCredential()
        {
            _admission.SetupClient(_manager, MatchId, PlayerId, Token);
            var request = await _admission.ClientPayload();
            Assert.AreSame(_admission, _manager.authenticator);
            Assert.AreEqual(MatchId, request.payload.matchId);
            Assert.AreEqual(PlayerId, request.payload.playerId);
            Assert.AreEqual(Token, request.payload.connectionToken);
            Assert.IsFalse((await _admission.Validate(new Connection(1), Payload())).success,
                "A client-only authenticator must never admit a peer.");
        }

        [Test]
        public void Setup_RejectsInvalidMatchAndMissingCredential()
        {
            Assert.Throws<ArgumentException>(() => _admission.SetupClient(_manager, "not-a-match", PlayerId, Token));
            Assert.Throws<ArgumentException>(() => _admission.SetupClient(_manager, MatchId, PlayerId, null));
            Assert.Throws<InvalidOperationException>(() => _admission.ClientPayload());
        }

        [Test]
        public async Task WrongMatch_IsRejectedBeforeCallingVerifier()
        {
            int calls = 0;
            Setup((_, _, _, _) => { calls++; return Task.FromResult(Accepted()); });
            var payload = Payload();
            payload.matchId = Guid.NewGuid().ToString();
            Assert.IsFalse((await _admission.Validate(new Connection(1), payload)).success);
            Assert.AreEqual(0, calls);
        }

        [TestCase(null, Token)]
        [TestCase("", Token)]
        [TestCase(PlayerId, null)]
        [TestCase(PlayerId, " ")]
        public async Task MissingCredential_IsRejectedBeforeCallingVerifier(string playerId, string token)
        {
            int calls = 0;
            Setup((_, _, _, _) => { calls++; return Task.FromResult(Accepted()); });
            var payload = Payload(playerId);
            payload.connectionToken = token;
            Assert.IsFalse((await _admission.Validate(new Connection(1), payload)).success);
            Assert.AreEqual(0, calls);
        }

        [Test]
        public async Task InvalidConnection_IsRejectedBeforeCallingVerifier()
        {
            int calls = 0;
            Setup((_, _, _, _) => { calls++; return Task.FromResult(Accepted()); });
            Assert.IsFalse((await _admission.Validate(default, Payload())).success);
            Assert.AreEqual(0, calls);
        }

        [Test]
        public async Task VerifiedAdmission_MapsIdentityAndTeamUntilDisconnect()
        {
            Setup((match, player, token, _) =>
            {
                Assert.AreEqual(MatchId, match);
                Assert.AreEqual(PlayerId, player);
                Assert.AreEqual(Token, token);
                return Task.FromResult(Accepted());
            });
            var connection = new Connection(1);
            var response = await _admission.Validate(connection, Payload());
            Assert.IsTrue(response.success);
            Assert.AreEqual(PlayerId, response.cookie);
            Assert.IsTrue(_admission.TryGetConnection(PlayerId, out var mapped));
            Assert.AreEqual(connection, mapped);
            Assert.IsTrue(_admission.TryGetPlayerID(connection, out var playerId));
            Assert.AreEqual(PlayerId, playerId);
            Assert.IsTrue(_admission.TryGetTeam(PlayerId, out var team));
            Assert.AreEqual(1, team);

            _admission.Disconnect(connection);
            Assert.IsFalse(_admission.TryGetConnection(PlayerId, out _));
            Assert.IsFalse(_admission.TryGetPlayerID(connection, out _));
            Assert.IsFalse(_admission.TryGetTeam(PlayerId, out _));
            Assert.IsTrue((await _admission.Validate(new Connection(2), Payload())).success);
        }

        [TestCase("other-player", 0)]
        [TestCase(PlayerId, null)]
        [TestCase(PlayerId, -1)]
        [TestCase(PlayerId, 2)]
        public async Task MismatchedOrIncompleteVerification_DoesNotMapIdentity(string verifiedPlayer, int? team)
        {
            Setup((_, _, _, _) => Task.FromResult(Accepted(verifiedPlayer, team)));
            Assert.IsFalse((await _admission.Validate(new Connection(1), Payload())).success);
            Assert.IsFalse(_admission.TryGetConnection(PlayerId, out _));
            Assert.IsFalse(_admission.TryGetTeam(PlayerId, out _));
        }

        [Test]
        public async Task RejectedOrUnavailableVerification_FailsClosed()
        {
            Setup((_, _, _, _) => Task.FromResult(new HttpResult<MatchmakingAdmission>
                { success = false, statusCode = 503, data = Accepted().data }));
            Assert.IsFalse((await _admission.Validate(new Connection(1), Payload())).success);
            Setup((_, _, _, _) => Task.FromResult(new HttpResult<MatchmakingAdmission>
                { success = true, data = new MatchmakingAdmission { valid = false, playerId = PlayerId, team = 0 } }));
            Assert.IsFalse((await _admission.Validate(new Connection(1), Payload())).success);
            Setup((_, _, _, _) => throw new InvalidOperationException("Verifier unavailable"));
            Assert.IsFalse((await _admission.Validate(new Connection(1), Payload())).success);
            Assert.IsFalse(_admission.TryGetConnection(PlayerId, out _));
        }

        [Test]
        public async Task DuplicatePlayer_IsRejectedDuringVerificationAndAfterAdmission()
        {
            var completion = new TaskCompletionSource<HttpResult<MatchmakingAdmission>>();
            int calls = 0;
            Setup((_, _, _, _) => { calls++; return completion.Task; });
            var first = _admission.Validate(new Connection(1), Payload());
            Assert.IsFalse((await _admission.Validate(new Connection(2), Payload())).success);
            Assert.AreEqual(1, calls);
            completion.SetResult(Accepted());
            Assert.IsTrue((await first).success);
            Assert.IsFalse((await _admission.Validate(new Connection(2), Payload())).success);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public async Task ConnectionCannotAuthenticateAsTwoPlayers()
        {
            int calls = 0;
            Setup((_, player, _, _) => { calls++; return Task.FromResult(Accepted(player)); });
            Assert.IsTrue((await _admission.Validate(new Connection(1), Payload())).success);
            Assert.IsFalse((await _admission.Validate(new Connection(1), Payload("other-player"))).success);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public async Task DisconnectedPendingPeer_CannotBeAdmittedByLateHttpResponse()
        {
            var completion = new TaskCompletionSource<HttpResult<MatchmakingAdmission>>();
            CancellationToken pending = default;
            Setup((_, _, _, token) => { pending = token; return completion.Task; });
            var validation = _admission.Validate(new Connection(1), Payload());
            _admission.Disconnect(new Connection(1));
            Assert.IsTrue(pending.IsCancellationRequested);
            completion.SetResult(Accepted());
            Assert.IsFalse((await validation).success);
            Assert.IsFalse(_admission.TryGetConnection(PlayerId, out _));
        }

        [Test]
        public async Task AdmissionDeadline_CancelsVerificationAndFailsClosed()
        {
            _admission.timeout = 0.1f;
            var completion = new TaskCompletionSource<HttpResult<MatchmakingAdmission>>();
            Setup((_, _, _, token) =>
            {
                token.Register(() => completion.TrySetResult(Accepted()));
                return completion.Task;
            });
            Assert.IsFalse((await _admission.Validate(new Connection(1), Payload())).success);
            Assert.IsFalse(_admission.TryGetConnection(PlayerId, out _));
        }

        [Test]
        public async Task ContextReset_FencesOldResponseAndPreservesNewPendingReservation()
        {
            var oldResponse = new TaskCompletionSource<HttpResult<MatchmakingAdmission>>();
            var newResponse = new TaskCompletionSource<HttpResult<MatchmakingAdmission>>();
            CancellationToken oldCancellation = default;
            Setup((_, _, _, token) => { oldCancellation = token; return oldResponse.Task; });
            var oldAttempt = _admission.Validate(new Connection(1), Payload());
            Setup((_, _, _, _) => newResponse.Task);
            Assert.IsTrue(oldCancellation.IsCancellationRequested);
            var newAttempt = _admission.Validate(new Connection(1), Payload());
            oldResponse.SetResult(Accepted());
            Assert.IsFalse((await oldAttempt).success);
            Assert.IsFalse((await _admission.Validate(new Connection(2), Payload())).success,
                "The old attempt's cleanup must not erase the replacement attempt's player reservation.");
            newResponse.SetResult(Accepted());
            Assert.IsTrue((await newAttempt).success);
            Assert.IsTrue(_admission.TryGetConnection(PlayerId, out var connection));
            Assert.AreEqual(new Connection(1), connection);
        }

        [Test]
        public async Task ResetContext_RemovesCredentialAndExistingIdentity()
        {
            Setup((_, _, _, _) => Task.FromResult(Accepted()));
            Assert.IsTrue((await _admission.Validate(new Connection(1), Payload())).success);
            _admission.ResetContext();
            Assert.IsFalse(_admission.TryGetConnection(PlayerId, out _));
            Assert.IsFalse(_admission.TryGetTeam(PlayerId, out _));
            Assert.Throws<InvalidOperationException>(() => _admission.ClientPayload());
            Assert.IsFalse((await _admission.Validate(new Connection(2), Payload())).success);
        }
    }
}
#endif
