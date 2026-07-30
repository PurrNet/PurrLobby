#if PURR_SERVICES
using System;
using PurrNet.Services;
using UnityEngine;

namespace PurrNet.Lobby.PurrNet
{
    public class PurrNetChat : LobbyChatBase, IDisposable
    {
        private readonly LobbyConnection _connection;

        private readonly PurrNetLobby _lobby;

        protected override IPlayer localPlayer => _lobby.localPlayer;

        public PurrNetChat(LobbyConnection connection, PurrNetLobby lobby)
        {
            _connection = connection;
            _lobby = lobby;
            _connection.onChat += OnChatMessage;
        }

        private void OnChatMessage(ChatMessage obj)
        {
            if (_lobby.TryGetPlayer(obj.playerId, out var player))
                ReceiveFromProvider(player, Convert.FromBase64String(obj.data));
        }

        protected override void SendToProvider(byte[] data)
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
    }
}
#endif
