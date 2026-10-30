using System.Collections.Generic;

namespace PurrNet.Lobby
{
    public struct MatchmakingRequest
    {
        public string gameMode;
        public Dictionary<string, string> attributes;
        /// <summary>Service queue ID. Empty uses the provider's configured queue.</summary>
        public string queueId;
        /// <summary>Intended party size; zero is treated as a solo search.</summary>
        public int partySize;
        /// <summary>Join an existing party ticket instead of creating one.</summary>
        public string partyTicketId;
        public string partyInviteCode;
        /// <summary>Measured latency in milliseconds, keyed by service region.</summary>
        public Dictionary<string, int> latencies;
    }
}
