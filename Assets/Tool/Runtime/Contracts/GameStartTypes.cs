using System.Collections.Generic;

namespace PurrLobby
{
    public struct GameStartRequest
    {
        public string lobbyId;
        public Dictionary<string, string> metadata;
    }
}
