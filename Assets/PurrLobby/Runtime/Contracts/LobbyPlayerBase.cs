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

        internal event Action<LobbyPlayerBase> updatedInternal;
        internal event Action<LobbyPlayerBase> metadataUpdatedInternal;

        private IMetadata _observedUserData;

        protected LobbyPlayerBase(string id, string displayName, bool isOwner)
        {
            this.id = id;
            this.displayName = displayName;
            this.isOwner = isOwner;
        }

        public virtual void SetReady(bool isReady)
        {
            ObserveUserDataChanges();
            userData?.SetData(LobbyPlayerKeys.ReadyKey,
                isReady ? LobbyPlayerKeys.ReadyTruthy : LobbyPlayerKeys.ReadyFalsy);
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
        public void NotifyUpdated()
        {
            onPlayerUpdated?.Invoke();
            updatedInternal?.Invoke(this);
        }

        /// <summary>Provider-internal: raises <see cref="onPlayerMetadataUpdated"/>.</summary>
        public void NotifyMetadataUpdated()
        {
            onPlayerMetadataUpdated?.Invoke();
            metadataUpdatedInternal?.Invoke(this);
        }

        /// <summary>Starts forwarding metadata diffs through the player events.</summary>
        internal void ObserveUserDataChanges()
        {
            var current = userData;
            if (ReferenceEquals(_observedUserData, current))
                return;

            StopObservingUserDataChanges();
            _observedUserData = current;

            if (_observedUserData != null)
                _observedUserData.onDataChanged += OnUserDataChanged;
        }

        internal void StopObservingUserDataChanges()
        {
            if (_observedUserData == null)
                return;

            _observedUserData.onDataChanged -= OnUserDataChanged;
            _observedUserData = null;
        }

        private void OnUserDataChanged(string key, string value)
        {
            NotifyMetadataUpdated();

            // Ready state is both metadata and a first-class player property. Keep
            // the general player event for compatibility without forwarding a
            // second duplicate lobby-level update.
            if (key == LobbyPlayerKeys.ReadyKey)
                onPlayerUpdated?.Invoke();
        }
    }
}
