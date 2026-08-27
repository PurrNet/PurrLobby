using UnityEngine;

namespace PurrNet.Lobby
{
    public abstract class LobbyConnectionProvider : MonoBehaviour
    {
        public abstract void JoinedLobby(ILobby lobby);

        public abstract void LeftLobby(ILobby lobby);

        public abstract void OnHostChanged(ILobby lobby, IPlayer host, bool isLocalPlayer);

        public abstract void OnPlayerRegistered(IPlayer player);

        public abstract void OnPlayerUnregistered(IPlayer player);
    }
}
