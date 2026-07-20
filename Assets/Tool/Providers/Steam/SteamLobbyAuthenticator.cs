#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif
#if STEAMWORKS && !DISABLESTEAMWORKS
using PurrNet.Steam;
using PurrNet.Transports;

namespace PurrNet.Lobby.Steam
{
    /// <summary>
    /// Lobby authenticator that additionally verifies the connecting peer's actual
    /// SteamID (as seen by the transport) matches the player id they claim, making
    /// the roster check spoof-proof.
    /// </summary>
    public class SteamLobbyAuthenticator : LobbyAuthenticator
    {
        protected override bool ValidateConnection(Connection conn, LobbyAuthPayload payload)
        {
            if (manager == null || manager.transport is not SteamTransport steamTransport)
                return true; // Not running over the Steam transport; the roster check stands alone.

            var actualSteamId = steamTransport.GetSteamID(conn);
            return actualSteamId != 0 && actualSteamId.ToString() == payload.playerId;
        }
    }
}
#endif
