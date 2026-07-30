using System.Collections.Generic;
using NUnit.Framework;

namespace PurrNet.Lobby.Tests
{
    public class LobbyBaseTests
    {
        private TestLobby _lobby;

        [SetUp]
        public void SetUp()
        {
            _lobby = new TestLobby();
        }

        [Test]
        public void AddPlayer_RaisesJoined_AndSetsLocal()
        {
            var joined = new List<IPlayer>();
            _lobby.onPlayerJoined += joined.Add;

            var alice = new TestPlayer("alice");
            _lobby.AddPlayerPublic(alice, isLocal: true);

            Assert.AreEqual(new IPlayer[] { alice }, joined);
            Assert.AreSame(alice, _lobby.localPlayer);
            Assert.AreEqual(1, _lobby.players.Count);
        }

        [Test]
        public void OnPlayerJoined_ReplaysExistingPlayersOnSubscribe()
        {
            var alice = new TestPlayer("alice");
            var bob = new TestPlayer("bob");
            _lobby.AddPlayerPublic(alice);
            _lobby.AddPlayerPublic(bob);

            var replayed = new List<IPlayer>();
            _lobby.onPlayerJoined += replayed.Add;

            Assert.AreEqual(new IPlayer[] { alice, bob }, replayed);
        }

        [Test]
        public void OnOwnerChanged_ReplaysCurrentOwnerOnSubscribe()
        {
            var alice = new TestPlayer("alice");
            _lobby.AddPlayerPublic(alice);
            _lobby.SetOwnerPublic(alice);

            var replayed = new List<IPlayer>();
            _lobby.onOwnerChanged += replayed.Add;

            Assert.AreEqual(new IPlayer[] { alice }, replayed);
        }

        [Test]
        public void OnOwnerChanged_NoOwner_NoReplay()
        {
            var replayed = new List<IPlayer>();
            _lobby.onOwnerChanged += replayed.Add;

            Assert.IsEmpty(replayed);
        }

        [Test]
        public void RemovePlayer_RaisesLeft_AndReturnsPlayer()
        {
            var alice = new TestPlayer("alice");
            _lobby.AddPlayerPublic(alice);

            var left = new List<IPlayer>();
            _lobby.onPlayerLeft += left.Add;

            Assert.IsTrue(_lobby.RemovePlayerPublic("alice", out var removed));
            Assert.AreSame(alice, removed);
            Assert.AreEqual(new IPlayer[] { alice }, left);
            Assert.AreEqual(0, _lobby.players.Count);

            Assert.IsFalse(_lobby.RemovePlayerPublic("alice", out _));
        }

        [Test]
        public void SetOwner_FlipsFlags_AndRaisesEventsOnlyForChanges()
        {
            var alice = new TestPlayer("alice", isOwner: true);
            var bob = new TestPlayer("bob");
            _lobby.AddPlayerPublic(alice, isLocal: true);
            _lobby.AddPlayerPublic(bob);
            _lobby.SetOwnerPublic(alice);

            var updated = new List<IPlayer>();
            var ownerChanges = new List<IPlayer>();
            _lobby.onPlayerUpdated += updated.Add;
            _lobby.onOwnerChanged += ownerChanges.Add;
            ownerChanges.Clear(); // drop the replay of the current owner

            _lobby.SetOwnerPublic(bob);

            Assert.IsFalse(alice.isOwner);
            Assert.IsTrue(bob.isOwner);
            Assert.AreSame(bob, _lobby.owner);
            // both players' flags changed, so both got an update event
            CollectionAssert.AreEquivalent(new IPlayer[] { alice, bob }, updated);
            Assert.AreEqual(new IPlayer[] { bob }, ownerChanges);
        }

        [Test]
        public void SetOwner_Null_ClearsWithoutOwnerChangedEvent()
        {
            var alice = new TestPlayer("alice", isOwner: true);
            _lobby.AddPlayerPublic(alice);
            _lobby.SetOwnerPublic(alice);

            var ownerChanges = new List<IPlayer>();
            _lobby.onOwnerChanged += ownerChanges.Add;
            ownerChanges.Clear();

            _lobby.SetOwnerPublic(null);

            Assert.IsNull(_lobby.owner);
            Assert.IsFalse(alice.isOwner);
            Assert.IsEmpty(ownerChanges);
        }

        [Test]
        public void IsOwner_TracksLocalPlayerFlag()
        {
            var alice = new TestPlayer("alice");
            _lobby.AddPlayerPublic(alice, isLocal: true);

            Assert.IsFalse(_lobby.isOwner);
            _lobby.SetOwnerPublic(alice);
            Assert.IsTrue(_lobby.isOwner);
        }

        [Test]
        public void RaisePlayerMetadataUpdated_RaisesPlayerAndLobbyEvents()
        {
            var alice = new TestPlayer("alice");
            _lobby.AddPlayerPublic(alice);

            int playerEvents = 0;
            var lobbyEvents = new List<IPlayer>();
            alice.onPlayerMetadataUpdated += () => playerEvents++;
            _lobby.onPlayerUpdated += lobbyEvents.Add;

            _lobby.RaisePlayerMetadataUpdatedPublic(alice);

            Assert.AreEqual(1, playerEvents);
            Assert.AreEqual(new IPlayer[] { alice }, lobbyEvents);
        }

        [Test]
        public void TryGetPlayer_FindsById()
        {
            var alice = new TestPlayer("alice");
            _lobby.AddPlayerPublic(alice);

            Assert.IsTrue(_lobby.TryGetPlayerPublic("alice", out var found));
            Assert.AreSame(alice, found);
            Assert.IsFalse(_lobby.TryGetPlayerPublic("bob", out _));
        }

        [Test]
        public void RaiseLobbyDestroyed_RaisesEvent()
        {
            int destroyed = 0;
            _lobby.onLobbyDestroyed += () => destroyed++;

            _lobby.RaiseLobbyDestroyedPublic();

            Assert.AreEqual(1, destroyed);
        }
    }

    public class LobbyPlayerBaseTests
    {
        [Test]
        public void SetReady_WritesConventionKey_AndNotifies()
        {
            var player = new TestPlayer("alice");
            int updates = 0;
            player.onPlayerUpdated += () => updates++;

            Assert.IsFalse(player.isReady);

            player.SetReady(true);
            Assert.IsTrue(player.isReady);
            Assert.AreEqual(LobbyPlayerKeys.ReadyTruthy, player.userData.GetData(LobbyPlayerKeys.ReadyKey));

            player.SetReady(false);
            Assert.IsFalse(player.isReady);
            Assert.AreEqual(2, updates);
        }

        [Test]
        public void SetIsOwner_ReportsChangesOnly()
        {
            var player = new TestPlayer("alice");

            Assert.IsTrue(player.SetIsOwner(true));
            Assert.IsTrue(player.isOwner);
            Assert.IsFalse(player.SetIsOwner(true));
            Assert.IsTrue(player.SetIsOwner(false));
        }
    }
}
