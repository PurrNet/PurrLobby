#if NAKAMA
using UnityEngine;

namespace PurrNet.Lobby.Nakama
{
    /// <summary>Chat implementation using Nakama match-state messages.</summary>
    public class NakamaChat : LobbyChatBase
    {
        private readonly NakamaLobby _lobby;

        protected override IPlayer localPlayer => _lobby.localPlayer;

        internal NakamaChat(NakamaLobby lobby)
        {
            _lobby = lobby;
        }

        protected override void SendToProvider(byte[] data)
        {
            try
            {
                _ = _lobby.SendMatchStateBytesAsync(NakamaOpCodes.Chat, data);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        internal void DispatchIncoming(IPlayer sender, byte[] state)
        {
            if (sender == null || state == null)
                return;
            ReceiveFromProvider(sender, state);
        }
    }
}
#endif
