#if NAKAMA
using System;
using UnityEngine;

namespace PurrNet.Lobby.Nakama
{
    /// <summary>Chat implementation using Nakama match-state messages.</summary>
    public class NakamaChat : ILobbyChat
    {
        private readonly NakamaLobby _lobby;

        public event Action<IPlayer, byte[]> onMessageReceived;

        internal NakamaChat(NakamaLobby lobby)
        {
            _lobby = lobby;
        }

        public void SendMessage(byte[] data)
        {
            if (data == null || data.Length == 0)
                return;

            try
            {
                _ = _lobby.SendMatchStateBytesAsync(NakamaOpCodes.Chat, data);

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
            onMessageReceived?.Invoke(sender, state);
        }
    }
}
#endif
