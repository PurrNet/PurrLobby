using NUnit.Framework;
using UnityEngine;

namespace PurrNet.Lobby.Tests
{
    public class HostMigrationLobbyConnectionTests
    {
        private GameObject _gameObject;
        private TestHostMigrationLobbyConnection _connection;
        private TestLobby _lobby;
        private TestPlayer _localPlayer;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("HostMigrationLobbyConnectionTests");
            _connection = _gameObject.AddComponent<TestHostMigrationLobbyConnection>();

            _lobby = new TestLobby();
            _localPlayer = new TestPlayer("local");
            _lobby.AddPlayerPublic(_localPlayer, isLocal: true);
            _connection.JoinedLobby(_lobby);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void RemotePlayerLeaving_KeepsLobbyConnectionActive()
        {
            var remotePlayer = new TestPlayer("remote");
            _lobby.AddPlayerPublic(remotePlayer);

            _lobby.RemovePlayerPublic(remotePlayer.id, out _);

            Assert.IsTrue(_connection.hasActiveLobbyConnectionPublic,
                "A remote departure may trigger host migration and must not suppress reconnects.");
        }

        [Test]
        public void LocalPlayerLeaving_DeactivatesLobbyConnection()
        {
            _lobby.RemovePlayerPublic(_localPlayer.id, out _);

            Assert.IsFalse(_connection.hasActiveLobbyConnectionPublic,
                "Losing local lobby membership is terminal and must suppress reconnects.");
        }

        [Test]
        public void LobbyDestroyed_DeactivatesLobbyConnection()
        {
            _lobby.RaiseLobbyDestroyedPublic();

            Assert.IsFalse(_connection.hasActiveLobbyConnectionPublic,
                "A destroyed lobby must suppress reconnects before the view finishes closing.");
        }

        [Test]
        public void JoiningAlreadyDestroyedLobby_DoesNotReactivateConnection()
        {
            var destroyedLobby = new TestLobby();
            destroyedLobby.AddPlayerPublic(new TestPlayer("other-local"), isLocal: true);
            destroyedLobby.RaiseLobbyDestroyedPublic();

            _connection.JoinedLobby(destroyedLobby);

            Assert.IsFalse(_connection.hasActiveLobbyConnectionPublic,
                "Replayed terminal state must win over connection setup.");
        }
    }
}
