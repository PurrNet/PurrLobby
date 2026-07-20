using System;
using UnityEngine;

namespace PurrNet.Lobby
{
    /// <summary>
    /// Shared <see cref="IPlayer"/> implementation: identity, ownership flag,
    /// ready-state convention and update events. Providers subclass this and
    /// supply the backing <see cref="userData"/> metadata.
    /// </summary>
    public abstract class LobbyPlayerBase : IPlayer
    {
        public string id { get; protected set; }

        public string displayName { get; protected set; }

        public virtual Texture2D avatar => null;

        public bool isOwner { get; private set; }

        public abstract IMetadata userData { get; }

        public bool isReady =>
            userData != null
            && userData.TryGetData(LobbyPlayerKeys.ReadyKey, out var ready)
            && ready == LobbyPlayerKeys.ReadyTruthy;

        public event Action onPlayerUpdated;
        public event Action onPlayerMetadataUpdated;

        protected LobbyPlayerBase(string id, string displayName, bool isOwner)
        {
            this.id = id;
            this.displayName = displayName;
            this.isOwner = isOwner;
        }

        public virtual void SetReady(bool isReady)
        {
            userData?.SetData(LobbyPlayerKeys.ReadyKey,
                isReady ? LobbyPlayerKeys.ReadyTruthy : LobbyPlayerKeys.ReadyFalsy);
            NotifyUpdated();
        }

        /// <summary>Sets the ownership flag; returns true if it changed. Called by the owning lobby.</summary>
        public bool SetIsOwner(bool value)
        {
            if (isOwner == value)
                return false;
            isOwner = value;
            return true;
        }

        /// <summary>Provider-internal: raises <see cref="onPlayerUpdated"/>.</summary>
        public void NotifyUpdated() => onPlayerUpdated?.Invoke();

        /// <summary>Provider-internal: raises <see cref="onPlayerMetadataUpdated"/>.</summary>
        public void NotifyMetadataUpdated() => onPlayerMetadataUpdated?.Invoke();
    }
}
