using PurrNet.Transports;
using UnityEngine;

namespace PurrNet.Lobby.PurrNet
{
    public class PurrTransportLobbyConnection : HostMigrationLobbyConnection
    {
        [SerializeField] private PurrTransport _transport;

        protected override void ConfigureTransportForHost(ILobby lobby, IPlayer host)
        {
            _transport.roomName = $"{lobby.id}_{host.id}";
        }
    }
}
