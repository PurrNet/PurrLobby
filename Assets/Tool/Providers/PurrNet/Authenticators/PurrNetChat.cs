using System;
using System.Text;
using PurrNet.Services;
using UnityEngine;

namespace PurrNet.Lobby.PurrNet
{
    public class PurrNetChat : ILobbyChat
    {
        private readonly LobbyConnection _connection;

        private readonly PurrNetLobby _lobby;

        public PurrNetChat(LobbyConnection connection, PurrNetLobby lobby)
        {
            _connection = connection;
            _lobby = lobby;
            _connection.onChat += OnChatMessage;
        }

        private void OnChatMessage(ChatMessage obj)
        {
            if (_lobby.TryGetPlayer(obj.playerId, out var player))
                onMessageReceived?.Invoke(player, obj.data);
        }

        public void SendMessage(string data)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(data);
                _ = PurrServices.instance.lobbies.SendChatAsync(_lobby.id, bytes);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public event Action<IPlayer, string> onMessageReceived;
    }
}
