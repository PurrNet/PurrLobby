using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace PurrLobby.Tests
{
    [TestFixture]
    public class MockLobbyTests
    {
        private class MockMetadata : IMetadata
        {
            private readonly Dictionary<string, string> _data = new Dictionary<string, string>();
            public event Action<string, string> onDataChanged;

            public void SetData(string key, string value)
            {
                _data[key] = value;
                onDataChanged?.Invoke(key, value);
            }

            public string GetData(string key)
            {
                return _data.TryGetValue(key, out var v) ? v : null;
            }

            public bool TryGetData(string key, out string value)
            {
                return _data.TryGetValue(key, out value);
            }

            public void RemoveData(string key)
            {
                _data.Remove(key);
                onDataChanged?.Invoke(key, null);
            }
        }

        private class MockPlayer : IPlayer
        {
            public string id { get; set; }
            public string displayName { get; set; }
            public bool isHost { get; set; }
            public IMetadata userData { get; set; }

            public MockPlayer(string id, string name, bool host = false)
            {
                this.id = id;
                displayName = name;
                isHost = host;
                userData = new MockMetadata();
            }
        }

        private class MockChat : ILobbyChat
        {
            public event Action<IPlayer, ArraySegment<byte>> onMessageReceived;

            public void SendMessage(ArraySegment<byte> data) { }

            public void SimulateReceive(IPlayer from, byte[] data)
            {
                onMessageReceived?.Invoke(from, new ArraySegment<byte>(data));
            }
        }

        private class MockLobby : ILobby
        {
            public string id { get; set; }
            public IPlayer localPlayer { get; set; }
            public IPlayer host { get; set; }
            public int maxPlayers { get; set; }

            private readonly List<IPlayer> _players = new List<IPlayer>();
            public IReadOnlyList<IPlayer> players => _players;

            public IMetadata lobbyData { get; set; }
            public ILobbyChat chat { get; set; }

            public event Action<IPlayer> onPlayerJoined;
            public event Action<IPlayer> onPlayerLeft;
            public event Action<IPlayer> onHostChanged;
            public event Action onLobbyDestroyed;

            public MockLobby()
            {
                lobbyData = new MockMetadata();
                chat = new MockChat();
            }

            public void AddPlayer(IPlayer player) => _players.Add(player);
            public void InvitePlayer(IPlayer player) { }
            public void KickPlayer(IPlayer player) => _players.Remove(player);
            public void LeaveLobby() { }

            public void SimulatePlayerJoined(IPlayer player)
            {
                _players.Add(player);
                onPlayerJoined?.Invoke(player);
            }

            public void SimulatePlayerLeft(IPlayer player)
            {
                _players.Remove(player);
                onPlayerLeft?.Invoke(player);
            }

            public void SimulateHostChanged(IPlayer newHost)
            {
                host = newHost;
                onHostChanged?.Invoke(newHost);
            }

            public void SimulateDestroyed()
            {
                onLobbyDestroyed?.Invoke();
            }
        }

        [Test]
        public void OnPlayerJoined_FiresCallback()
        {
            var lobby = new MockLobby();
            IPlayer received = null;
            lobby.onPlayerJoined += p => received = p;

            var player = new MockPlayer("p1", "Alice");
            lobby.SimulatePlayerJoined(player);

            Assert.IsNotNull(received);
            Assert.AreEqual("p1", received.id);
            Assert.AreEqual("Alice", received.displayName);
        }

        [Test]
        public void OnPlayerLeft_FiresCallback()
        {
            var lobby = new MockLobby();
            var player = new MockPlayer("p1", "Alice");
            lobby.AddPlayer(player);

            IPlayer received = null;
            lobby.onPlayerLeft += p => received = p;
            lobby.SimulatePlayerLeft(player);

            Assert.IsNotNull(received);
            Assert.AreEqual("p1", received.id);
        }

        [Test]
        public void OnHostChanged_FiresCallback()
        {
            var lobby = new MockLobby();
            IPlayer received = null;
            lobby.onHostChanged += p => received = p;

            var newHost = new MockPlayer("p2", "Bob", true);
            lobby.SimulateHostChanged(newHost);

            Assert.IsNotNull(received);
            Assert.AreEqual("p2", received.id);
            Assert.AreEqual(lobby.host, newHost);
        }

        [Test]
        public void OnLobbyDestroyed_FiresCallback()
        {
            var lobby = new MockLobby();
            bool destroyed = false;
            lobby.onLobbyDestroyed += () => destroyed = true;

            lobby.SimulateDestroyed();
            Assert.IsTrue(destroyed);
        }

        [Test]
        public void PlayerList_TracksJoinAndLeave()
        {
            var lobby = new MockLobby();
            Assert.AreEqual(0, lobby.players.Count);

            var p1 = new MockPlayer("p1", "Alice");
            var p2 = new MockPlayer("p2", "Bob");

            lobby.SimulatePlayerJoined(p1);
            Assert.AreEqual(1, lobby.players.Count);

            lobby.SimulatePlayerJoined(p2);
            Assert.AreEqual(2, lobby.players.Count);

            lobby.SimulatePlayerLeft(p1);
            Assert.AreEqual(1, lobby.players.Count);
            Assert.AreEqual("p2", lobby.players[0].id);
        }

        [Test]
        public void KickPlayer_RemovesFromList()
        {
            var lobby = new MockLobby();
            var player = new MockPlayer("p1", "Alice");
            lobby.AddPlayer(player);

            Assert.AreEqual(1, lobby.players.Count);
            lobby.KickPlayer(player);
            Assert.AreEqual(0, lobby.players.Count);
        }

        [Test]
        public void LobbyMetadata_SetAndGet()
        {
            var lobby = new MockLobby();
            lobby.lobbyData.SetData("map", "forest");

            Assert.AreEqual("forest", lobby.lobbyData.GetData("map"));
        }

        [Test]
        public void LobbyMetadata_TryGetData()
        {
            var lobby = new MockLobby();
            lobby.lobbyData.SetData("mode", "pvp");

            Assert.IsTrue(lobby.lobbyData.TryGetData("mode", out var val));
            Assert.AreEqual("pvp", val);

            Assert.IsFalse(lobby.lobbyData.TryGetData("missing", out _));
        }

        [Test]
        public void LobbyMetadata_RemoveData()
        {
            var lobby = new MockLobby();
            lobby.lobbyData.SetData("key", "value");
            lobby.lobbyData.RemoveData("key");

            Assert.IsNull(lobby.lobbyData.GetData("key"));
        }

        [Test]
        public void LobbyMetadata_OnDataChanged_Fires()
        {
            var lobby = new MockLobby();
            string changedKey = null;
            string changedValue = null;
            lobby.lobbyData.onDataChanged += (k, v) => { changedKey = k; changedValue = v; };

            lobby.lobbyData.SetData("test", "123");
            Assert.AreEqual("test", changedKey);
            Assert.AreEqual("123", changedValue);
        }

        [Test]
        public void PlayerMetadata_SetAndGet()
        {
            var player = new MockPlayer("p1", "Alice");
            player.userData.SetData("level", "5");

            Assert.AreEqual("5", player.userData.GetData("level"));
        }

        [Test]
        public void Chat_OnMessageReceived_Fires()
        {
            var lobby = new MockLobby();
            var mockChat = (MockChat)lobby.chat;
            var sender = new MockPlayer("p1", "Alice");

            IPlayer receivedFrom = null;
            byte[] receivedData = null;
            lobby.chat.onMessageReceived += (p, d) =>
            {
                receivedFrom = p;
                receivedData = new byte[d.Count];
                Array.Copy(d.Array, d.Offset, receivedData, 0, d.Count);
            };

            var msg = System.Text.Encoding.UTF8.GetBytes("hello");
            mockChat.SimulateReceive(sender, msg);

            Assert.IsNotNull(receivedFrom);
            Assert.AreEqual("p1", receivedFrom.id);
            Assert.AreEqual("hello", System.Text.Encoding.UTF8.GetString(receivedData));
        }

        [Test]
        public void MockPlayer_Properties()
        {
            var player = new MockPlayer("id-1", "TestPlayer", true);
            Assert.AreEqual("id-1", player.id);
            Assert.AreEqual("TestPlayer", player.displayName);
            Assert.IsTrue(player.isHost);
            Assert.IsNotNull(player.userData);
        }

        [Test]
        public void MultipleEventSubscribers_AllFire()
        {
            var lobby = new MockLobby();
            int count = 0;
            lobby.onPlayerJoined += _ => count++;
            lobby.onPlayerJoined += _ => count++;

            lobby.SimulatePlayerJoined(new MockPlayer("p1", "Alice"));
            Assert.AreEqual(2, count);
        }
    }
}
