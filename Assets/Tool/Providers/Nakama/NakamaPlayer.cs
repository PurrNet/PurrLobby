#if NAKAMA
using System;
using UnityEngine;

namespace PurrNet.Lobby.Nakama
{
    public class NakamaPlayer : IPlayer
    {
        public string id { get; }

        public string displayName { get; private set; }

        public Texture2D avatar => null;

        public bool isOwner { get; private set; }

        public bool isReady =>
            userData != null
            && userData.TryGetData(IPlayer.READY_KEY, out var ready)
            && ready == IPlayer.READY_TRUTHY_VALUE;

        public IMetadata userData => _metadata;

        public event Action onPlayerUpdated;
        public event Action onPlayerMetadataUpdated;

        private readonly NakamaMetadata _metadata;
        private readonly NakamaLobby _lobby;

        internal NakamaPlayer(NakamaLobby lobby, string userId, string displayName, bool isHost, bool isLocal)
        {
            _lobby = lobby;
            id = userId;
            this.displayName = displayName;
            this.isOwner = isHost;
            _metadata = new NakamaMetadata(lobby, userId, isLocal);
        }

        internal void SetIsHost(bool value)
        {
            if (isOwner == value)
                return;
            isOwner = value;
        }

        internal NakamaMetadata GetMetadata() => _metadata;

        public void SetReady(bool isReady)
        {
            userData.SetData(IPlayer.READY_KEY, isReady ? IPlayer.READY_TRUTHY_VALUE : "0");
            TriggerOnPlayerUpdated();
        }

        internal void TriggerOnPlayerUpdated() => onPlayerUpdated?.Invoke();

        internal void TriggerOnPlayerMetadataUpdated() => onPlayerMetadataUpdated?.Invoke();
    }
}
#endif
