using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace PurrLobby.Tests
{
    [TestFixture]
    public class ContractTests
    {
        [Test]
        public void ILobby_DoesNotHave_StartGame()
        {
            var type = typeof(ILobby);
            var method = type.GetMethod("StartGame");
            Assert.IsNull(method, "ILobby should not have a StartGame method");
        }

        [Test]
        public void ILobby_DoesNotHave_OnGameStarted()
        {
            var type = typeof(ILobby);
            var evt = type.GetEvent("onGameStarted");
            Assert.IsNull(evt, "ILobby should not have an onGameStarted event");
        }

        [Test]
        public void ILobbyProvider_Has_JoinRandom()
        {
            var type = typeof(ILobbyProvider);
            var method = type.GetMethod("JoinRandom");
            Assert.IsNotNull(method, "ILobbyProvider should have JoinRandom");
        }

        [Test]
        public void ILobbyProvider_DoesNotHave_QuickJoin()
        {
            var type = typeof(ILobbyProvider);
            var method = type.GetMethod("QuickJoin");
            Assert.IsNull(method, "ILobbyProvider should not have QuickJoin");
        }

        [Test]
        public void IGameStarter_StartGame_Returns_TaskConnectionInfo()
        {
            var type = typeof(IGameStarter);
            var method = type.GetMethod("StartGame");
            Assert.IsNotNull(method, "IGameStarter should have StartGame");
            Assert.AreEqual(typeof(Task<ConnectionInfo>), method.ReturnType);
        }

        [Test]
        public void IGameStarter_StartGame_Has_CancellationToken_Parameter()
        {
            var method = typeof(IGameStarter).GetMethod("StartGame");
            var parameters = method.GetParameters();
            Assert.AreEqual(2, parameters.Length);
            Assert.AreEqual(typeof(GameStartRequest), parameters[0].ParameterType);
            Assert.AreEqual(typeof(CancellationToken), parameters[1].ParameterType);
        }

        [Test]
        public void MatchResult_Has_ConnectionInfo_Field()
        {
            var field = typeof(MatchResult).GetField("connection");
            Assert.IsNotNull(field, "MatchResult should have a 'connection' field");
            Assert.AreEqual(typeof(ConnectionInfo), field.FieldType);
        }

        [Test]
        public void MatchResult_DoesNotHave_FlatConnectionFields()
        {
            var type = typeof(MatchResult);
            Assert.IsNull(type.GetField("serverAddress"), "MatchResult should not have flat serverAddress");
            Assert.IsNull(type.GetField("serverPort"), "MatchResult should not have flat serverPort");
            Assert.IsNull(type.GetField("connectionToken"), "MatchResult should not have flat connectionToken");
        }

        [Test]
        public void GameStartKeys_Address_HasCorrectValue()
        {
            Assert.AreEqual("_game.address", GameStartKeys.Address);
        }

        [Test]
        public void GameStartKeys_Port_HasCorrectValue()
        {
            Assert.AreEqual("_game.port", GameStartKeys.Port);
        }

        [Test]
        public void GameStartKeys_Token_HasCorrectValue()
        {
            Assert.AreEqual("_game.token", GameStartKeys.Token);
        }

        [Test]
        public void GameStartKeys_Status_HasCorrectValue()
        {
            Assert.AreEqual("_game.status", GameStartKeys.Status);
        }

        [Test]
        public void ILobby_Has_Expected_Properties()
        {
            var type = typeof(ILobby);
            Assert.IsNotNull(type.GetProperty("id"), "ILobby should have 'id'");
            Assert.IsNotNull(type.GetProperty("localPlayer"), "ILobby should have 'localPlayer'");
            Assert.IsNotNull(type.GetProperty("host"), "ILobby should have 'host'");
            Assert.IsNotNull(type.GetProperty("maxPlayers"), "ILobby should have 'maxPlayers'");
            Assert.IsNotNull(type.GetProperty("players"), "ILobby should have 'players'");
            Assert.IsNotNull(type.GetProperty("lobbyData"), "ILobby should have 'lobbyData'");
            Assert.IsNotNull(type.GetProperty("chat"), "ILobby should have 'chat'");
        }

        [Test]
        public void ILobby_Has_Expected_Events()
        {
            var type = typeof(ILobby);
            Assert.IsNotNull(type.GetEvent("onPlayerJoined"), "ILobby should have 'onPlayerJoined'");
            Assert.IsNotNull(type.GetEvent("onPlayerLeft"), "ILobby should have 'onPlayerLeft'");
            Assert.IsNotNull(type.GetEvent("onHostChanged"), "ILobby should have 'onHostChanged'");
            Assert.IsNotNull(type.GetEvent("onLobbyDestroyed"), "ILobby should have 'onLobbyDestroyed'");
        }

        [Test]
        public void IPlayer_Has_Expected_Properties()
        {
            var type = typeof(IPlayer);
            Assert.IsNotNull(type.GetProperty("id"));
            Assert.IsNotNull(type.GetProperty("displayName"));
            Assert.IsNotNull(type.GetProperty("isHost"));
            Assert.IsNotNull(type.GetProperty("userData"));
        }

        [Test]
        public void ILobbyProvider_Has_Expected_Methods()
        {
            var type = typeof(ILobbyProvider);
            Assert.IsNotNull(type.GetMethod("CreateLobby"));
            Assert.IsNotNull(type.GetMethod("JoinLobby"));
            Assert.IsNotNull(type.GetMethod("JoinLobbyByCode"));
            Assert.IsNotNull(type.GetMethod("JoinRandom"));
            Assert.IsNotNull(type.GetMethod("QueryLobbies"));
        }
    }
}
