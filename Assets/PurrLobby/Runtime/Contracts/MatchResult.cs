namespace PurrNet.Lobby
{
    public struct MatchResult
    {
        public ILobby lobby;
        public ConnectionInfo connection;
        public bool isHost;
        public string matchId;
        public string playerId;
        public string region;
        public bool ranked;
        /// <summary>The elected player must start listening and acknowledge readiness before other clients join.</summary>
        public bool requiresHostReady;
    }
}
