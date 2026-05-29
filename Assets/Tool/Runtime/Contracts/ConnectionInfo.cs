using System.Collections.Generic;

namespace PurrNet.Lobby
{
    public struct ConnectionInfo
    {
        public string serverAddress;
        public int serverPort;
        public string connectionToken;
        public string hostId;
        public Dictionary<string, string> metadata;

        public override string ToString()
        {
            return $"serverAddress: {serverAddress}, serverPort: {serverPort}, connectionToken: {connectionToken}, hostId: {hostId}, metadata: {metadata}";
        }
    }
}
