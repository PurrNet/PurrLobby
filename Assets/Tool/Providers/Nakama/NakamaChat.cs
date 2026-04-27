#if NAKAMA
using System;
using System.Text;
using UnityEngine;

namespace PurrNet.Lobby.Nakama
{
    /// <summary>
    /// Chat is broadcast as a UTF-8 byte payload using <see cref="NakamaOpCodes.Chat"/>. The sender is
    /// identified by the <c>UserPresence</c> field on the inbound match-state message, so we only encode
    /// the message body itself.
    /// </summary>
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

                // Nakama's relayed SendMatchStateAsync does not echo to the sender, so fire
                // the local message ourselves to match the PurrNet chat contract where the
                // sender sees their own messages.
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
