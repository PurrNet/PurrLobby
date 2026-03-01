using System.Collections.Generic;

namespace PurrLobby
{
    public struct ConnectionInfo
    {
        public string serverAddress;
        public int serverPort;
        public string connectionToken;
        public Dictionary<string, string> metadata;
    }
}
