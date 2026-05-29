using System;

namespace PurrNet.Lobby
{
    public interface ILobbyChat
    {
        void SendMessage(byte[] data);

        event Action<IPlayer, byte[]> onMessageReceived;
    }
}
