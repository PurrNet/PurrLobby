#if PURR_SERVICES_MATCHMAKING
using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PurrNet.Lobby.PurrNet;
using PurrNet.Transports;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PurrNet.Lobby.Tests
{
    public class TestServicesGameAllocator : PurrServicesMatchGameAllocator
    {
        public bool Configure(NetworkManager manager, ConnectionInfo connection) => ConfigureTransport(manager, connection, true);
    }

    public class PurrServicesMatchAllocatorTests
    {
        class LobbyOwner : LobbyPlayerBase
        {
            public LobbyOwner() : base("owner", "owner", true) { }
            public override IMetadata userData => null;
        }

        class OrdinaryLobby : LobbyBase<LobbyOwner>
        {
            public OrdinaryLobby()
            {
                var owner = new LobbyOwner();
                AddPlayer(owner, true);
                SetOwner(owner);
            }
            public override string id => "ordinary-lobby";
            public override int maxPlayers => 4;
            public override IMetadata lobbyData => null;
            public override bool isLobbyJoinable => true;
            public override ILobbyChat chat => null;
            public override void KickPlayer(IPlayer player) { }
            public override void SetIsLobbyJoinable(bool isJoinable) { }
            public override void LeaveLobby() { }
        }

        GameObject _object;
        NetworkManager _manager;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Match transport tests");
            _object.SetActive(false);
            _manager = _object.AddComponent<NetworkManager>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_object);

        [Test]
        public void DirectTransport_UsesAssignedEndpoint()
        {
            PurrServicesMatchGameAllocator.ConfigureMatchTransport(_manager,
                new ConnectionInfo { serverAddress = "192.0.2.1", serverPort = 27841 }, PurrServicesMatchTransport.DirectUdp);
            Assert.IsInstanceOf<UDPTransport>(_manager.transport);
            var transport = (UDPTransport)_manager.transport;
            Assert.AreEqual("192.0.2.1", transport.address);
            Assert.AreEqual(27841, transport.serverPort);
        }

        [Test]
        public void RelayTransport_UsesAssignedRoom()
        {
            PurrServicesMatchGameAllocator.ConfigureMatchTransport(_manager,
                new ConnectionInfo { serverAddress = "match-test-room" }, PurrServicesMatchTransport.Relay, true);
            Assert.IsInstanceOf<PurrTransport>(_manager.transport);
            var transport = (PurrTransport)_manager.transport;
            Assert.AreEqual("match-test-room", transport.roomName);
            Assert.IsTrue(transport.useNat);
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(65536)]
        public void DirectTransport_RejectsInvalidPort(int port)
        {
            var existing = _manager.transport;
            var existingUdp = existing as UDPTransport;
            var existingPort = existingUdp ? existingUdp.serverPort : 0;
            var existingAddress = existingUdp ? existingUdp.address : null;
            Assert.Throws<ArgumentException>(() => PurrServicesMatchGameAllocator.ConfigureMatchTransport(_manager,
                new ConnectionInfo { serverAddress = "192.0.2.1", serverPort = port }, PurrServicesMatchTransport.DirectUdp));
            Assert.AreSame(existing, _manager.transport, "Invalid input must leave the existing transport untouched.");
            if (existingUdp)
            {
                Assert.AreEqual(existingPort, existingUdp.serverPort);
                Assert.AreEqual(existingAddress, existingUdp.address);
            }
        }

        [TestCase(PurrServicesMatchTransport.Relay)]
        [TestCase(PurrServicesMatchTransport.DirectUdp)]
        public void Transport_RejectsMissingAddress(PurrServicesMatchTransport transport)
        {
            Assert.Throws<ArgumentException>(() => PurrServicesMatchGameAllocator.ConfigureMatchTransport(_manager,
                new ConnectionInfo { serverPort = 7777 }, transport));
        }

        [Test]
        public async Task MissingAdmissionCredential_PreventsAllocation()
        {
            var allocator = ScriptableObject.CreateInstance<PurrServicesMatchGameAllocator>();
            try
            {
                var response = await allocator.AllocateGame(new MatchResult
                {
                    matchId = "92db3997-e783-43f4-b6db-d691a8fa51d7", playerId = "player-a",
                    connection = new ConnectionInfo { serverAddress = "room" }
                });
                Assert.IsFalse(response.success);
            }
            finally { Object.DestroyImmediate(allocator); }
        }

        [Test]
        public async Task OrdinaryLobby_PreservesRelayGameAllocation()
        {
            var allocator = ScriptableObject.CreateInstance<TestServicesGameAllocator>();
            try
            {
                // Ranked direct-server configuration must not change the
                // ordinary lobby's existing relay hosting flow.
                var settings = new UnityEditor.SerializedObject(allocator);
                settings.FindProperty("_transport").enumValueIndex = (int)PurrServicesMatchTransport.DirectUdp;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var response = await allocator.AllocateGame(new OrdinaryLobby());
                Assert.IsTrue(response.success);
                Assert.AreEqual("PURRLOBBY_GAME_ordinary-lobby", response.connection.serverAddress);
                Assert.AreEqual("owner", response.connection.hostId);
                Assert.IsTrue(allocator.Configure(_manager, response.connection));
                Assert.IsInstanceOf<PurrTransport>(_manager.transport);
            }
            finally { Object.DestroyImmediate(allocator); }
        }

        [Test]
        public void RelaySelection_DefaultQueueAliasUsesEuropeCentral()
        {
            var chosen = PurrServicesMatchGameAllocator.SelectRelayServer("eu", new[]
            {
                new RelayServer { region = "us-east", host = "us.example.invalid" },
                new RelayServer { region = "eu-central", host = "eu.example.invalid" }
            });
            Assert.AreEqual("eu-central", chosen.region);
            Assert.AreEqual("eu.example.invalid", chosen.host);
        }

        [Test]
        public void RelaySelection_UsesProjectAliasAndExactUnmappedRegion()
        {
            var servers = new[]
            {
                new RelayServer { region = "us-east", host = "us.example.invalid" },
                new RelayServer { region = "eu-central", host = "eu.example.invalid" }
            };
            var aliases = new[] { new MatchmakingRelayRegion { matchmakingRegion = "north-america", relayRegion = "us-east" } };
            Assert.AreEqual("us-east", PurrServicesMatchGameAllocator.SelectRelayServer("north-america", servers, aliases).region);
            Assert.AreEqual("eu-central", PurrServicesMatchGameAllocator.SelectRelayServer("eu-central", servers, aliases).region);
        }

        [Test]
        public void RelaySelection_UnavailableMatchedRegionDoesNotChooseAnotherRegion()
        {
            var error = Assert.Throws<InvalidOperationException>(() => PurrServicesMatchGameAllocator.SelectRelayServer("eu", new[]
            {
                new RelayServer { region = "us-east", host = "us.example.invalid" }
            }));
            StringAssert.Contains("eu-central", error.Message);
            Assert.Throws<InvalidOperationException>(() => PurrServicesMatchGameAllocator.SelectRelayServer("eu", new[]
            {
                new RelayServer { region = "eu-central", host = " " }
            }));
        }

        [Test]
        public void RelaySelection_InvalidRegionConfigurationFailsBeforeLookup()
        {
            Assert.Throws<ArgumentException>(() => PurrServicesMatchGameAllocator.ResolveRelayRegion(null));
            Assert.Throws<ArgumentException>(() => PurrServicesMatchGameAllocator.ResolveRelayRegion("eu", new[]
            {
                new MatchmakingRelayRegion { matchmakingRegion = "eu", relayRegion = "" }
            }));
            Assert.Throws<ArgumentException>(() => PurrServicesMatchGameAllocator.ResolveRelayRegion("eu", new[]
            {
                new MatchmakingRelayRegion { matchmakingRegion = "eu", relayRegion = "eu-central" },
                new MatchmakingRelayRegion { matchmakingRegion = "eu", relayRegion = "us-east" }
            }));
        }

        [Test]
        public async Task RelayPreparation_UsesConfiguredMatchedRelayWithoutHttp()
        {
            PurrServicesMatchGameAllocator.ConfigureMatchTransport(_manager,
                new ConnectionInfo { serverAddress = "match-test-room" }, PurrServicesMatchTransport.Relay);
            var relay = (PurrTransport)_manager.transport;
            relay.masterServer = "invalid-network-address";
            relay.SetServer(new RelayServer { region = "eu-central", host = "eu.example.invalid" });
            await PurrServicesMatchGameAllocator.PrepareRelayRegionAsync(_manager, "eu");
            Assert.AreEqual("eu-central", relay.region);
            Assert.AreEqual("eu.example.invalid", relay.host);
            Assert.AreEqual(ConnectionState.Disconnected, _manager.serverState);
        }

        [Test]
        public void RelayPreparation_CancelledStartupDoesNotLookupOrChangeTransport()
        {
            PurrServicesMatchGameAllocator.ConfigureMatchTransport(_manager,
                new ConnectionInfo { serverAddress = "match-test-room" }, PurrServicesMatchTransport.Relay);
            var relay = (PurrTransport)_manager.transport;
            relay.masterServer = "invalid-network-address";
            relay.SetServer(new RelayServer { region = "us-east", host = "us.example.invalid" });
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Assert.CatchAsync<OperationCanceledException>(async () =>
                await PurrServicesMatchGameAllocator.PrepareRelayRegionAsync(_manager, "eu", cancellationToken: cancelled.Token));
            Assert.AreEqual("us-east", relay.region);
            Assert.AreEqual(ConnectionState.Disconnected, _manager.serverState);
        }
    }
}
#endif
