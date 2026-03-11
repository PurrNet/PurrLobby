using System.Collections.Generic;

namespace PurrNet.Lobby
{
    public struct MatchmakingRequest
    {
        public string gameMode;
        public Dictionary<string, string> attributes;
    }
}
