#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif
#if STEAMWORKS && !DISABLESTEAMWORKS
using PurrNet.Steam;
using UnityEngine;

namespace PurrNet.Lobby.Steam
{
    /// <summary>
    /// Host migration for Steam lobbies: Steam auto-migrates lobby ownership, and
    /// this re-points the transport at the new owner's SteamID before restarting.
    /// </summary>
    public class SteamLobbyConnection : HostMigrationLobbyConnection
    {
        [SerializeField] private SteamTransport _transport;

        protected override void ConfigureTransportForHost(ILobby lobby, IPlayer host)
        {
            var transport = ResolveTransport();
            if (!transport)
                return;

            transport.peerToPeer = true;
            transport.dedicatedServer = false;
            transport.address = host.id;
        }

        private SteamTransport ResolveTransport()
        {
            if (_transport)
                return _transport;
            if (networkManager && networkManager.transport is SteamTransport t)
                _transport = t;
            return _transport;
        }
    }
}
#endif
