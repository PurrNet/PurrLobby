using UnityEngine;

namespace PurrLobby
{
    public interface IPlayer
    {
        public const string READY_KEY = "PLAYER_READY_KEY";
        public const string READY_TRUTHY_VALUE = "1";

        string id { get; }

        string displayName { get; }

        Texture2D avatar { get; }

        bool isHost { get; }

        bool isReady { get; }

        IMetadata userData { get; }

        public event System.Action onPlayerUpdated;

        public event System.Action onPlayerMetadataUpdated;
    }
}
