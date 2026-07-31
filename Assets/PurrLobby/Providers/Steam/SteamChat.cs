#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif
#if STEAMWORKS && !DISABLESTEAMWORKS
using Steamworks;
using UnityEngine;

namespace PurrNet.Lobby.Steam
{
    /// <summary>Chat over Steam lobby messages.</summary>
    public class SteamChat : LobbyChatBase
    {
        private readonly SteamLobby _lobby;

        protected override IPlayer localPlayer => _lobby.localPlayer;

        internal SteamChat(SteamLobby lobby)
        {
            _lobby = lobby;
        }

        protected override void SendToProvider(byte[] data)
        {
            if (!SteamMatchmaking.SendLobbyChatMsg(_lobby.steamLobbyId, data, data.Length))
                Debug.LogWarning("[SteamChat] Failed to send lobby chat message.");
        }

        /// <summary>Called by SteamLobby when a LobbyChatMsg_t arrives.</summary>
        internal void DispatchIncoming(IPlayer sender, byte[] data)
        {
            ReceiveFromProvider(sender, data);
        }
    }
}
#endif
