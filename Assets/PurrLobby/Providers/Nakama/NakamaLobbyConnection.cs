#if NAKAMA
using PurrNet.Nakama;
using UnityEngine;

namespace PurrNet.Lobby.Nakama
{
    /// <summary>
    /// Manages the NetworkManager lifecycle for Nakama lobbies, including host migration.
    /// The lobby and gameplay share a single Nakama match, so migration reuses the match id and
    /// only re-designates which peer acts as the PurrNet host.
    /// </summary>
    public class NakamaLobbyConnection : HostMigrationLobbyConnection
    {
        [SerializeField] private NakamaTransport _transport;

        protected override void ConfigureTransportForHost(ILobby lobby, IPlayer host)
        {
            var transport = ResolveTransport();
            if (!transport)
                return;

            transport.socket = NakamaConnection.instance.socket;
            transport.matchId = lobby.id;
            transport.hostUserId = host.id;
        }

        private NakamaTransport ResolveTransport()
        {
            if (_transport)
                return _transport;
            if (networkManager && networkManager.transport is NakamaTransport t)
                _transport = t;
            return _transport;
        }
    }
}
#endif
