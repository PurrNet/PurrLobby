using System.Collections.Generic;
using NUnit.Framework;

namespace PurrLobby.Tests
{
    [TestFixture]
    public class LobbyTypesTests
    {
        [Test]
        public void ConnectionInfo_DefaultValues()
        {
            var info = new ConnectionInfo();
            Assert.IsNull(info.serverAddress);
            Assert.AreEqual(0, info.serverPort);
            Assert.IsNull(info.connectionToken);
            Assert.IsNull(info.metadata);
        }

        [Test]
        public void ConnectionInfo_SetAndReadFields()
        {
            var info = new ConnectionInfo
            {
                serverAddress = "127.0.0.1",
                serverPort = 7777,
                connectionToken = "abc123",
                metadata = new Dictionary<string, string> { { "region", "eu" } }
            };

            Assert.AreEqual("127.0.0.1", info.serverAddress);
            Assert.AreEqual(7777, info.serverPort);
            Assert.AreEqual("abc123", info.connectionToken);
            Assert.AreEqual("eu", info.metadata["region"]);
        }

        [Test]
        public void GameStartRequest_DefaultValues()
        {
            var req = new GameStartRequest();
            Assert.IsNull(req.lobbyId);
            Assert.IsNull(req.metadata);
        }

        [Test]
        public void GameStartRequest_SetAndReadFields()
        {
            var req = new GameStartRequest
            {
                lobbyId = "lobby-42",
                metadata = new Dictionary<string, string> { { "mode", "deathmatch" } }
            };

            Assert.AreEqual("lobby-42", req.lobbyId);
            Assert.AreEqual("deathmatch", req.metadata["mode"]);
        }

        [Test]
        public void MatchResult_DefaultValues()
        {
            var result = new MatchResult();
            Assert.IsNull(result.lobbyId);
            Assert.AreEqual(default(ConnectionInfo), result.connection);
        }

        [Test]
        public void MatchResult_SetAndReadFields()
        {
            var result = new MatchResult
            {
                lobbyId = "lobby-99",
                connection = new ConnectionInfo
                {
                    serverAddress = "10.0.0.1",
                    serverPort = 9999
                }
            };

            Assert.AreEqual("lobby-99", result.lobbyId);
            Assert.AreEqual("10.0.0.1", result.connection.serverAddress);
            Assert.AreEqual(9999, result.connection.serverPort);
        }

        [Test]
        public void LobbySettings_DefaultValues()
        {
            var settings = new LobbySettings();
            Assert.IsNull(settings.name);
            Assert.AreEqual(0, settings.maxPlayers);
            Assert.AreEqual(LobbyVisibility.Public, settings.visibility);
        }

        [Test]
        public void LobbySettings_SetAndReadFields()
        {
            var settings = new LobbySettings
            {
                name = "Test Lobby",
                maxPlayers = 8,
                visibility = LobbyVisibility.Private
            };

            Assert.AreEqual("Test Lobby", settings.name);
            Assert.AreEqual(8, settings.maxPlayers);
            Assert.AreEqual(LobbyVisibility.Private, settings.visibility);
        }

        [Test]
        public void LobbyQuery_DefaultValues()
        {
            var query = new LobbyQuery();
            Assert.AreEqual(0, query.maxResults);
            Assert.IsNull(query.dataFilters);
        }

        [Test]
        public void LobbyQuery_SetAndReadFields()
        {
            var query = new LobbyQuery
            {
                maxResults = 25,
                dataFilters = new Dictionary<string, string> { { "map", "desert" } }
            };

            Assert.AreEqual(25, query.maxResults);
            Assert.AreEqual("desert", query.dataFilters["map"]);
        }

        [Test]
        public void LobbyInfo_DefaultValues()
        {
            var info = new LobbyInfo();
            Assert.IsNull(info.id);
            Assert.IsNull(info.name);
            Assert.IsNull(info.code);
            Assert.AreEqual(0, info.playerCount);
            Assert.AreEqual(0, info.maxPlayers);
            Assert.IsNull(info.metadata);
        }

        [Test]
        public void LobbyInfo_SetAndReadFields()
        {
            var info = new LobbyInfo
            {
                id = "lobby-1",
                name = "Fun Lobby",
                code = "ABCD",
                playerCount = 3,
                maxPlayers = 4,
                metadata = new Dictionary<string, string> { { "version", "1.0" } }
            };

            Assert.AreEqual("lobby-1", info.id);
            Assert.AreEqual("Fun Lobby", info.name);
            Assert.AreEqual("ABCD", info.code);
            Assert.AreEqual(3, info.playerCount);
            Assert.AreEqual(4, info.maxPlayers);
            Assert.AreEqual("1.0", info.metadata["version"]);
        }

        [Test]
        public void LobbyVisibility_HasExpectedValues()
        {
            Assert.AreEqual(0, (int)LobbyVisibility.Public);
            Assert.AreEqual(1, (int)LobbyVisibility.Private);
        }
    }
}
