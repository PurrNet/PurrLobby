using System;

namespace PurrLobby
{
    public interface ILobbyChat
    {
        void SendMessage(ArraySegment<byte> data);

        event System.Action<IPlayer, ArraySegment<byte>> onMessageReceived;
    }
}