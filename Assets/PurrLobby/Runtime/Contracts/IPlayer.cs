using UnityEngine;

namespace PurrNet.Lobby
{
    public interface IPlayer
    {
        string id { get; }

        string displayName { get; }

        Texture2D avatar { get; }

        bool isOwner { get; }

        bool isReady { get; }

        IMetadata userData { get; }

        public event System.Action onPlayerUpdated;

        public event System.Action onPlayerMetadataUpdated;

        public void SetReady(bool isReady);
    }
}
