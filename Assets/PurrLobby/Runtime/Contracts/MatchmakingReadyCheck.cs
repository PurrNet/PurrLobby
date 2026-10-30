namespace PurrNet.Lobby
{
    /// <summary>A server-owned ready check. Respond explicitly through the provider.</summary>
    public struct MatchmakingReadyCheck
    {
        public string matchId;
        /// <summary>UTC deadline in ISO 8601 form.</summary>
        public string deadline;
        public int acceptedPlayers;
        public int totalPlayers;
        public bool accepted;
    }
}
