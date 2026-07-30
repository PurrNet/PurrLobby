#if PURR_SERVICES
using System;
using PurrNet.Services;
using UnityEngine;

namespace PurrNet.Lobby.PurrNet
{
    public class PurrNetChat : ILobbyChat, IDisposable
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
                onMessageReceived?.Invoke(player, Convert.FromBase64String(obj.data));
        }

        public void SendMessage(byte[] data)
        {
            try
            {
                PurrServices.instance.lobbies.SendChatAsync(_lobby.id, data)
                    .Forget("[PurrNetChat] SendChat failed");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public void Dispose()
        {
            _connection.onChat -= OnChatMessage;
        }

        public event Action<IPlayer, byte[]> onMessageReceived;
    }
}
#endif
