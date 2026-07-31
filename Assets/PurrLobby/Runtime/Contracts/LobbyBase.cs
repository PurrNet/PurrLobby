using System;
using System.Collections.Generic;

namespace PurrNet.Lobby
{
    /// <summary>
    /// Shared <see cref="ILobby"/> implementation: player roster, ownership tracking
    /// and the standardized replay-on-subscribe event semantics — subscribing to
    /// <see cref="onPlayerJoined"/> immediately replays all current players and
    /// subscribing to <see cref="onOwnerChanged"/> replays the current owner.
    /// </summary>
    public abstract class LobbyBase<TPlayer> : ILobby where TPlayer : LobbyPlayerBase
    {
        protected readonly List<TPlayer> playerList = new();

        protected TPlayer localPlayerInternal;
        protected TPlayer ownerInternal;

        public abstract string id { get; }

        public virtual string joinCode => id;

        public IPlayer localPlayer => localPlayerInternal;

        public IPlayer owner => ownerInternal;

        public abstract int maxPlayers { get; }

        public IReadOnlyList<IPlayer> players => playerList;

        public abstract IMetadata lobbyData { get; }

        public abstract bool isLobbyJoinable { get; }

        public abstract ILobbyChat chat { get; }

        public bool isOwner => localPlayerInternal != null && localPlayerInternal.isOwner;

        public abstract void KickPlayer(IPlayer player);

        public abstract void SetIsLobbyJoinable(bool isJoinable);

        public abstract void LeaveLobby();

        private Action<IPlayer> _onPlayerJoined;
        public event Action<IPlayer> onPlayerJoined
        {
            add
            {
                _onPlayerJoined += value;
                if (value == null)
                    return;
                for (int i = 0; i < playerList.Count; i++)
                    value.Invoke(playerList[i]);
            }
            remove => _onPlayerJoined -= value;
        }

        private Action<IPlayer> _onOwnerChanged;
        public event Action<IPlayer> onOwnerChanged
        {
            add
            {
                _onOwnerChanged += value;
                if (value != null && ownerInternal != null)
                    value.Invoke(ownerInternal);
            }
            remove => _onOwnerChanged -= value;
        }

        private Action<IPlayer> _onPlayerLeft;
        public event Action<IPlayer> onPlayerLeft
        {
            add => _onPlayerLeft += value;
            remove => _onPlayerLeft -= value;
        }

        private Action<IPlayer> _onPlayerUpdated;
        public event Action<IPlayer> onPlayerUpdated
        {
            add => _onPlayerUpdated += value;
            remove => _onPlayerUpdated -= value;
        }

        private Action _onLobbyDestroyed;
        public event Action onLobbyDestroyed
        {
            add => _onLobbyDestroyed += value;
            remove => _onLobbyDestroyed -= value;
        }

        /// <summary>Adds a player to the roster and raises <see cref="onPlayerJoined"/>.</summary>
        protected void AddPlayer(TPlayer player, bool isLocal)
        {
            playerList.Add(player);
            player.updatedInternal += OnPlayerUpdatedInternal;
            player.metadataUpdatedInternal += OnPlayerMetadataUpdatedInternal;
            player.ObserveUserDataChanges();
            if (isLocal)
                localPlayerInternal = player;
            _onPlayerJoined?.Invoke(player);
        }

        /// <summary>Removes a player by id and raises <see cref="onPlayerLeft"/>.</summary>
        protected bool RemovePlayer(string playerId, out TPlayer removed)
        {
            for (int i = 0; i < playerList.Count; i++)
            {
                if (playerList[i].id == playerId)
                {
                    removed = playerList[i];
                    playerList.RemoveAt(i);
                    removed.StopObservingUserDataChanges();
                    removed.updatedInternal -= OnPlayerUpdatedInternal;
                    removed.metadataUpdatedInternal -= OnPlayerMetadataUpdatedInternal;
                    _onPlayerLeft?.Invoke(removed);
                    return true;
                }
            }

            removed = null;
            return false;
        }

        protected bool TryGetPlayerInternal(string playerId, out TPlayer player)
        {
            for (int i = 0; i < playerList.Count; i++)
            {
                if (playerList[i].id == playerId)
                {
                    player = playerList[i];
                    return true;
                }
            }

            player = null;
            return false;
        }

        /// <summary>
        /// Sets the owner (null allowed while the new owner is unknown), flips the
        /// ownership flags on the roster — raising <see cref="onPlayerUpdated"/> for
        /// players whose flag changed — and raises <see cref="onOwnerChanged"/>.
        /// </summary>
        protected void SetOwner(TPlayer newOwner)
        {
            ownerInternal = newOwner;

            for (int i = 0; i < playerList.Count; i++)
            {
                var player = playerList[i];
                if (player.SetIsOwner(newOwner != null && player == newOwner))
                    RaisePlayerUpdated(player);
            }

            if (newOwner != null)
                _onOwnerChanged?.Invoke(newOwner);
        }

        /// <summary>Raises the player's own update event and the lobby-level <see cref="onPlayerUpdated"/>.</summary>
        protected void RaisePlayerUpdated(TPlayer player)
        {
            player.NotifyUpdated();
        }

        /// <summary>Raises the player's metadata event and the lobby-level <see cref="onPlayerUpdated"/>.</summary>
        protected void RaisePlayerMetadataUpdated(TPlayer player)
        {
            player.NotifyMetadataUpdated();
        }

        private void OnPlayerUpdatedInternal(LobbyPlayerBase player) =>
            _onPlayerUpdated?.Invoke((TPlayer)player);

        private void OnPlayerMetadataUpdatedInternal(LobbyPlayerBase player) =>
            _onPlayerUpdated?.Invoke((TPlayer)player);

        protected void RaiseLobbyDestroyed() => _onLobbyDestroyed?.Invoke();
    }
}
