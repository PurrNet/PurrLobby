#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif
#if STEAMWORKS && !DISABLESTEAMWORKS
using System;
using Steamworks;
using UnityEngine;

namespace PurrNet.Lobby.Steam
{
    /// <summary>
    /// Chat over Steam lobby messages. Steam echoes the sender's own message back
    /// through LobbyChatMsg_t, so no manual local echo is needed.
    /// </summary>
    public class SteamChat : ILobbyChat
    {
        public event Action<IPlayer, byte[]> onMessageReceived;

        private readonly SteamLobby _lobby;

        internal SteamChat(SteamLobby lobby)
        {
            _lobby = lobby;
        }

        public void SendMessage(byte[] data)
        {
            if (data == null || data.Length == 0)
                return;

            if (!SteamMatchmaking.SendLobbyChatMsg(_lobby.steamLobbyId, data, data.Length))
                Debug.LogWarning("[SteamChat] Failed to send lobby chat message.");
        }

        /// <summary>Called by SteamLobby when a LobbyChatMsg_t arrives.</summary>
        internal void DispatchIncoming(IPlayer sender, byte[] data)
        {
            onMessageReceived?.Invoke(sender, data);
        }
    }
}
#endif
