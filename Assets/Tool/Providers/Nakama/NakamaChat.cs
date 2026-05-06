#if NAKAMA
using System;
using System.Text;
using UnityEngine;

namespace PurrNet.Lobby.Nakama
{
    /// <summary>Chat implementation using Nakama match-state messages.</summary>
    public class NakamaChat : ILobbyChat
    {
        private readonly NakamaLobby _lobby;

        public event Action<IPlayer, string> onMessageReceived;

        internal NakamaChat(NakamaLobby lobby)
        {
            _lobby = lobby;
        }

        public void SendMessage(string data)
        {
            if (string.IsNullOrEmpty(data))
                return;

            try
            {
                var bytes = Encoding.UTF8.GetBytes(data);
                _ = _lobby.SendMatchStateBytesAsync(NakamaOpCodes.Chat, bytes);

                // Nakama relayed matches don't echo to sender; fire locally
                if (_lobby.localPlayer != null)
                    onMessageReceived?.Invoke(_lobby.localPlayer, data);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        internal void DispatchIncoming(IPlayer sender, byte[] state)
        {
            if (sender == null || state == null)
                return;
            try
            {
                var text = Encoding.UTF8.GetString(state);
                onMessageReceived?.Invoke(sender, text);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
#endif
