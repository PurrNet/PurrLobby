#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif
using System.Threading.Tasks;
using PurrNet.UI;
using UnityEngine;
#if STEAMWORKS && !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace PurrNet.Lobby.Steam
{
    /// <summary>
    /// Steam authentication is ambient — the running Steam client is the session.
    /// Login just initializes the SteamAPI; there is nothing to log out of.
    /// </summary>
    [CreateAssetMenu(menuName = "PurrLobby/Steam/Session Provider", fileName = "Steam Session Provider", order = -203)]
    public class SteamSessionProvider : SessionProvider
    {
#if STEAMWORKS && !DISABLESTEAMWORKS
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
#else
        public override bool isLoggedIn => false;

        public override string playerId => null;

        public override string playerName => null;

        public override Task Login(ViewStack stack)
        {
            Debug.LogError(
                $"[{name}] Steamworks.NET is not installed or Steam is unsupported on this platform.");
            return Task.CompletedTask;
        }

        public override Task Logout() => Task.CompletedTask;
#endif
    }
}
