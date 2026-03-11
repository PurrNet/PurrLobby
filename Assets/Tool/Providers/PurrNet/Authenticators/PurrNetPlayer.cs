using System;
using PurrNet.Services;
using UnityEngine;

namespace PurrLobby.PurrNet
{
    public class PurrNetPlayer : IPlayer
    {
        public string id { get; private set; }

        public string displayName { get; private set;  }

        public Texture2D avatar { get; private set; }

        public bool isHost { get; private set; }

        public bool isReady
        {
            get
            {
                return userData != null && userData.TryGetData(IPlayer.READY_KEY, out var ready)
                                        && ready == IPlayer.READY_TRUTHY_VALUE;
            }
        }

        public IMetadata userData { get; }

        public event Action onPlayerUpdated;

        public event Action onPlayerMetadataUpdated;

        public PurrNetPlayer(LobbyService service, string lobbyId)
        {
            userData = new PurrNetMetadata(service, lobbyId, true);
        }

        public void SetIsHost(bool isHost)
        {
            this.isHost = isHost;
        }

        internal bool Update(string hostId, LobbyPlayer player)
        {
            bool changed = false;

            if (player.id != id)
            {
                id = player.id;
                changed = true;
            }

            if (player.displayName != displayName)
            {
                displayName = player.displayName;
                changed = true;
            }

            if (isHost != (id == hostId))
            {
                isHost = id == hostId;
                changed = true;
            }

            return changed;
        }

        public void TriggerOnPlayerUpdated()
        {
            onPlayerUpdated?.Invoke();
        }

        public void TriggerOnPlayerMetadataUpdated()
        {
            onPlayerMetadataUpdated?.Invoke();
        }
    }
}
