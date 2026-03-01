using System.Collections.Generic;

namespace PurrLobby
{
    public struct MatchmakingRequest
    {
        public string gameMode;
        public Dictionary<string, string> attributes;
    }
}