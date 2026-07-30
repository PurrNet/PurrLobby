#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif
#if STEAMWORKS && !DISABLESTEAMWORKS
using System.Threading.Tasks;
using PurrNet.UI;
using Steamworks;
using UnityEngine;

namespace PurrNet.Lobby.Steam
{
    /// <summary>
    /// Steam authentication is ambient — the running Steam client is the session.
    /// Login just initializes the SteamAPI; there is nothing to log out of.
    /// </summary>
    [CreateAssetMenu(menuName = "PurrLobby/Steam/Session Provider", fileName = "Steam Session Provider", order = -203)]
    public class SteamSessionProvider : SessionProvider
    {
        public override bool isLoggedIn => SteamRuntime.isInitialized;

        public override string playerId =>
            SteamRuntime.isInitialized ? SteamRuntime.localSteamId.m_SteamID.ToString() : null;

        public override string playerName =>
            SteamRuntime.isInitialized ? SteamFriends.GetPersonaName() : null;

        public override Task Login(ViewStack stack)
        {
            if (!SteamRuntime.EnsureInitialized())
                Debug.LogError($"[{name}] Steam is not available. Lobby features will fail until Steam is running.");

            return Task.CompletedTask;
        }

        public override Task Logout() => Task.CompletedTask;
    }
}
#endif
