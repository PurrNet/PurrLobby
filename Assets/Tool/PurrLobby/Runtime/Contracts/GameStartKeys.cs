using UnityEngine;

namespace PurrNet.Lobby
{
    public static class GameStartKeys
    {
        public const string Status = "_game.status";

        /// <summary>Lobby metadata key carrying the host's serialized <see cref="ConnectionInfo"/>.</summary>
        public const string ConnInfo = "LOBBY_CONN_INFO";

        /// <summary>Publishes the host's connection info to the lobby so joiners can connect.</summary>
        public static void PublishConnectionInfo(ILobby lobby, ConnectionInfo info)
        {
            lobby.lobbyData.SetData(ConnInfo, JsonUtility.ToJson(info));
        }

        /// <summary>Parses a value read from the <see cref="ConnInfo"/> metadata key.</summary>
        public static bool TryReadConnectionInfo(string rawValue, out ConnectionInfo info)
        {
            if (string.IsNullOrEmpty(rawValue))
            {
                info = default;
                return false;
            }

            try
            {
                info = JsonUtility.FromJson<ConnectionInfo>(rawValue);
                return true;
            }
            catch
            {
                info = default;
                return false;
            }
        }
    }
}
