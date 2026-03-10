using System;

namespace PurrLobby
{
    public interface ILobbyChat
    {
        void SendMessage(string data);

        event Action<IPlayer, string> onMessageReceived;
    }
}
