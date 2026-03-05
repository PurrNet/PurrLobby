namespace PurrLobby
{
    internal sealed class PurrPlayer : IPlayer
    {
        public string id { get; }
        public string displayName { get; internal set; }
        public bool isHost { get; internal set; }
        public IMetadata userData { get; }

        internal long joinedAt;
        internal long lastSeen;

        public PurrPlayer(string id, string displayName, bool isHost, long joinedAt, long lastSeen)
        {
            this.id = id;
            this.displayName = displayName;
            this.isHost = isHost;
            this.joinedAt = joinedAt;
            this.lastSeen = lastSeen;
            userData = new PurrMetadata();
        }
    }
}
