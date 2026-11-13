using NUnit.Framework;

namespace PurrNet.Lobby.Tests
{
    public class LobbyPlayerBindingsTests
    {
        private const float Timeout = 10f;

        private TestLobby _lobby;
        private TestPlayer _host;
        private TestPlayer _alice;
        private TestPlayer _mallory;
        private LobbyPlayerBindings _bindings;

        private static readonly PlayerID HostConnection = new PlayerID(1, false);
        private static readonly PlayerID AliceConnection = new PlayerID(2, false);
        private static readonly PlayerID MalloryConnection = new PlayerID(3, false);
        private static readonly PlayerID AliceReconnection = new PlayerID(4, false);

        [SetUp]
        public void SetUp()
        {
            // The game host's view of the lobby: its own member is local, the others are remote.
            _lobby = new TestLobby();
            _host = new TestPlayer("host", isOwner: true);
            _alice = new TestPlayer("alice");
            _mallory = new TestPlayer("mallory");
            _lobby.AddPlayerPublic(_host, isLocal: true);
            _lobby.AddPlayerPublic(_alice);
            _lobby.AddPlayerPublic(_mallory);

            _bindings = new LobbyPlayerBindings(_lobby, Timeout);
        }

        /// <summary>
        /// What the lobby service does when a member writes its own member data: the change shows up in
        /// every member's copy, including the host's. Only that member can write it.
        /// </summary>
        private static void MemberWritesOwnData(TestPlayer member, string key, string value) =>
            ((TestMetadata)member.userData).ApplyPatch(new System.Collections.Generic.Dictionary<string, string> { { key, value } });

        /// <summary>The genuine client side: answer the host's challenge in our own member data, then claim.</summary>
        private void GenuineJoin(PlayerID connection, TestPlayer member, float now = 0f)
        {
            var nonce = _bindings.Challenge(connection, now);
            MemberWritesOwnData(member, LobbyPlayerBindings.ProofKey, nonce);
            _bindings.Claim(connection, member.id);
        }

        [Test]
        public void GenuineClient_IsBoundToItsLobbyPlayer()
        {
            GenuineJoin(AliceConnection, _alice);
            _bindings.Update(0f);

            Assert.IsTrue(_bindings.TryGetLobbyPlayer(AliceConnection, out var lobbyPlayer));
            Assert.AreSame(_alice, lobbyPlayer);
            Assert.IsTrue(_bindings.TryGetPlayerID(_alice.id, out var connection));
            Assert.AreEqual(AliceConnection, connection);
        }

        [Test]
        public void ClaimIsNotTrusted_UntilTheMemberDataShowsTheNonce()
        {
            _bindings.Challenge(AliceConnection, 0f);
            _bindings.Claim(AliceConnection, _alice.id);
            _bindings.Update(0f);

            Assert.IsFalse(_bindings.TryGetLobbyPlayer(AliceConnection, out _),
                "A bare claim must not bind; the member's own data has to confirm it.");
        }

        [Test]
        public void ForgedClaim_OfAnotherMembersId_IsNeverBound()
        {
            // Mallory's game connection claims Alice's lobby id (readable by every member from lobby.players).
            var malloryNonce = _bindings.Challenge(MalloryConnection, 0f);
            _bindings.Claim(MalloryConnection, _alice.id);
            // Mallory can only write her own member data, not Alice's.
            MemberWritesOwnData(_mallory, LobbyPlayerBindings.ProofKey, malloryNonce);
            _bindings.Update(0f);

            Assert.IsFalse(_bindings.TryGetLobbyPlayer(MalloryConnection, out _));
            Assert.IsFalse(_bindings.TryGetPlayerID(_alice.id, out _));

            // Alice's real connection still binds afterwards, and Mallory's stays unbound.
            GenuineJoin(AliceConnection, _alice);
            _bindings.Update(1f);

            Assert.IsTrue(_bindings.TryGetPlayerID(_alice.id, out var aliceConnection));
            Assert.AreEqual(AliceConnection, aliceConnection);
            Assert.IsFalse(_bindings.TryGetLobbyPlayer(MalloryConnection, out _));
        }

        [Test]
        public void ForgedClaim_ThenDisconnect_DoesNotAffectTheRealMember()
        {
            // The attack from the report: claim the victim's id before the victim connects, then drop.
            _bindings.Challenge(MalloryConnection, 0f);
            _bindings.Claim(MalloryConnection, _alice.id);
            _bindings.Update(0f);

            IPlayer unbound = null;
            _bindings.onUnbound += (_, p) => unbound = p;
            _bindings.Remove(MalloryConnection);

            Assert.IsNull(unbound, "Mallory was never Alice, so her disconnect must not report Alice as gone.");

            GenuineJoin(AliceConnection, _alice);
            _bindings.Update(1f);
            Assert.IsTrue(_bindings.TryGetLobbyPlayer(AliceConnection, out var p));
            Assert.AreSame(_alice, p);
        }

        [Test]
        public void CopiedNonce_InAnotherMembersData_DoesNotBindThem()
        {
            // Alice answers honestly. Mallory copies Alice's (now public) nonce into her own data and claims
            // herself on her own connection, hoping the copied nonce counts for it.
            var aliceNonce = _bindings.Challenge(AliceConnection, 0f);
            _bindings.Challenge(MalloryConnection, 0f);
            MemberWritesOwnData(_alice, LobbyPlayerBindings.ProofKey, aliceNonce);
            MemberWritesOwnData(_mallory, LobbyPlayerBindings.ProofKey, aliceNonce);
            _bindings.Claim(AliceConnection, _alice.id);
            _bindings.Claim(MalloryConnection, _mallory.id);
            _bindings.Update(0f);

            Assert.IsTrue(_bindings.TryGetLobbyPlayer(AliceConnection, out var p));
            Assert.AreSame(_alice, p);
            Assert.IsFalse(_bindings.TryGetLobbyPlayer(MalloryConnection, out _),
                "Each connection gets its own nonce; someone else's nonce proves nothing.");
        }

        [Test]
        public void OldNonce_FromAPreviousConnection_DoesNotBindANewOne()
        {
            GenuineJoin(AliceConnection, _alice);
            _bindings.Update(0f);
            _bindings.Remove(AliceConnection);

            // A new connection (anyone's) gets a fresh nonce; Alice's member data still shows the old one.
            _bindings.Challenge(MalloryConnection, 1f);
            _bindings.Claim(MalloryConnection, _alice.id);
            _bindings.Update(1f);

            Assert.IsFalse(_bindings.TryGetLobbyPlayer(MalloryConnection, out _));
        }

        [Test]
        public void OnlyTheFirstClaimOfAConnectionCounts()
        {
            var nonce = _bindings.Challenge(MalloryConnection, 0f);
            _bindings.Claim(MalloryConnection, _mallory.id);
            _bindings.Claim(MalloryConnection, _alice.id);
            MemberWritesOwnData(_mallory, LobbyPlayerBindings.ProofKey, nonce);
            _bindings.Update(0f);

            Assert.IsTrue(_bindings.TryGetLobbyPlayer(MalloryConnection, out var p));
            Assert.AreSame(_mallory, p);
        }

        [Test]
        public void SlowMemberData_StillBindsWithinTheTimeout()
        {
            var nonce = _bindings.Challenge(AliceConnection, 0f);
            _bindings.Claim(AliceConnection, _alice.id);
            _bindings.Update(Timeout * 0.5f);
            Assert.IsFalse(_bindings.TryGetLobbyPlayer(AliceConnection, out _));

            MemberWritesOwnData(_alice, LobbyPlayerBindings.ProofKey, nonce);
            _bindings.Update(Timeout * 0.9f);

            Assert.IsTrue(_bindings.TryGetLobbyPlayer(AliceConnection, out _));
        }

        [Test]
        public void UnansweredChallenge_Expires()
        {
            string failed = null;
            _bindings.onBindingFailed += (_, claimed) => failed = claimed ?? "<none>";

            var nonce = _bindings.Challenge(AliceConnection, 0f);
            _bindings.Claim(AliceConnection, _alice.id);
            _bindings.Update(Timeout + 0.1f);

            Assert.AreEqual(_alice.id, failed);
            MemberWritesOwnData(_alice, LobbyPlayerBindings.ProofKey, nonce);
            _bindings.Update(Timeout + 0.2f);
            Assert.IsFalse(_bindings.TryGetLobbyPlayer(AliceConnection, out _), "An expired challenge can't bind later.");
        }

        [Test]
        public void Reconnect_MovesTheBindingToTheNewConnection()
        {
            GenuineJoin(AliceConnection, _alice);
            _bindings.Update(0f);

            GenuineJoin(AliceReconnection, _alice, 1f);
            _bindings.Update(1f);

            Assert.IsFalse(_bindings.TryGetLobbyPlayer(AliceConnection, out _));
            Assert.IsTrue(_bindings.TryGetPlayerID(_alice.id, out var connection));
            Assert.AreEqual(AliceReconnection, connection);
        }

        [Test]
        public void Host_BindsItsOwnConnectionDirectly()
        {
            _bindings.BindLocal(HostConnection);

            Assert.IsTrue(_bindings.TryGetLobbyPlayer(HostConnection, out var p));
            Assert.AreSame(_host, p);
        }

        [Test]
        public void ClaimOfTheHostsId_IsNeverBound()
        {
            _bindings.BindLocal(HostConnection);

            var nonce = _bindings.Challenge(MalloryConnection, 0f);
            _bindings.Claim(MalloryConnection, _host.id);
            MemberWritesOwnData(_mallory, LobbyPlayerBindings.ProofKey, nonce);
            _bindings.Update(0f);

            Assert.IsFalse(_bindings.TryGetLobbyPlayer(MalloryConnection, out _));
            Assert.IsTrue(_bindings.TryGetPlayerID(_host.id, out var connection));
            Assert.AreEqual(HostConnection, connection);
        }

        [Test]
        public void MemberLeavingTheLobby_RemovesItsBinding()
        {
            GenuineJoin(AliceConnection, _alice);
            _bindings.Update(0f);

            _bindings.OnLobbyPlayerLeft(_alice);

            Assert.IsFalse(_bindings.TryGetLobbyPlayer(AliceConnection, out _));
            Assert.IsFalse(_bindings.TryGetPlayerID(_alice.id, out _));
        }

        [Test]
        public void Disconnect_RaisesUnboundWithTheLobbyPlayer()
        {
            GenuineJoin(AliceConnection, _alice);
            _bindings.Update(0f);

            PlayerID gone = default;
            IPlayer goneMember = null;
            _bindings.onUnbound += (id, p) => { gone = id; goneMember = p; };
            _bindings.Remove(AliceConnection);

            Assert.AreEqual(AliceConnection, gone);
            Assert.AreSame(_alice, goneMember);
            Assert.IsFalse(_bindings.TryGetPlayerID(_alice.id, out _));
        }
    }
}
