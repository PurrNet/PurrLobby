namespace PurrLobby
{
    public interface IPlayer
    {
        string id { get; }

        string displayName { get; }

        bool isHost { get; }

        IMetadata userData { get; }

        public event System.Action onPlayerUpdated;

        public event System.Action onPlayerMetadataUpdated;
    }
}
