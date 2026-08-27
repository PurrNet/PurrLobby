using System;

namespace PurrNet.Lobby
{
    /// <summary>
    /// How to reach a game server. <see cref="serverAddress"/> is transport-specific:
    /// a host address for direct transports, a room/match id for relayed ones, or a
    /// host SteamID for Steam peer-to-peer.
    /// </summary>
    [Serializable]
    public struct ConnectionInfo
    {
        public string serverAddress;
        public int serverPort;

        /// <summary>Opaque token an allocator may attach (e.g. a matchmaker ticket id). Transports are free to ignore it.</summary>
        public string connectionToken;

        /// <summary>Player id of the hosting player, when a player hosts.</summary>
        public string hostId;

        public override string ToString()
        {
            return $"serverAddress: {serverAddress}, serverPort: {serverPort}, connectionToken: {connectionToken}, hostId: {hostId}";
        }
    }
}
