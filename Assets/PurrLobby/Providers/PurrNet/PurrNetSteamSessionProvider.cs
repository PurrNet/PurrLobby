#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif
using System;
using System.Threading.Tasks;
using PurrNet.UI;
using UnityEngine;
#if PURR_SERVICES_STEAM && STEAMWORKS && !DISABLESTEAMWORKS
using PurrNet.Lobby.Steam;
using PurrNet.Services;
#endif

namespace PurrNet.Lobby.PurrNet
{
    /// <summary>
    /// Signs players in to PurrServices with their Steam account instead of a device id; the
    /// player id is <c>steam:&lt;steamid64&gt;</c>. Pair it with the PurrNet lobby provider.
    /// The project needs Steam sign-in turned on (purrnet.dev → project → Auth → Steam), either
    /// verified by Steam (App ID + publisher Web API key) or trusting the game (no setup, but
    /// anyone can claim any Steam account). The game code is the same for both.
    /// </summary>
    [ProviderDependency("dev.purrnet.services", "PurrServices")]
    [ProviderDependency("com.rlabrecque.steamworks.net", "Steamworks.NET")]
    [CreateAssetMenu(menuName = "PurrLobby/PurrNet/Steam Session Provider",
        fileName = "Steam Session Provider (PurrServices)", order = -201)]
    public class PurrNetSteamSessionProvider : SessionProvider
    {
#if PURR_SERVICES_STEAM && STEAMWORKS && !DISABLESTEAMWORKS
        private const string SignInFailed = "Could not sign in with Steam. Please try again later.";

        public override bool isLoggedIn => PurrServices.instance.auth.isAuthenticated;

        public override string playerId => PurrServices.instance.auth.playerId;

        public override string playerName => PurrServices.instance.auth.displayName;

        public override async Task Login(ViewStack stack)
        {
            var services = PurrServices.instance;
            if (!PurrServicesConfiguration.IsConfigured(services))
            {
                Toaster.PushError("Online Services Unavailable", PurrServicesConfiguration.UserError);
                throw new InvalidOperationException(PurrServicesConfiguration.DeveloperError);
            }

            if (!SteamRuntime.EnsureInitialized())
            {
                Toaster.PushError("Steam Unavailable", "Start Steam and restart the game to play online.");
                throw new InvalidOperationException($"[{name}] Steam could not be initialized; sign-in was not started.");
            }

            var steamPlayerId = "steam:" + SteamRuntime.localSteamId.m_SteamID;

            if (services.auth.isAuthenticated)
            {
                var session = await services.auth.ValidateSessionAsync();

                if (session.success && session.playerId == steamPlayerId)
                    return;

                // A saved session for someone else: another Steam account on this machine,
                // or a device login from a different session provider. Never reuse it.
                if (session.success)
                    services.auth.Logout();
                else
                    Debug.LogWarning($"[{name}] Saved session is no longer valid: {session.error}");
            }

            var result = await PurrSteamAuth.LoginAsync();

            if (result.success)
                return;

            Toaster.PushError("Steam Sign-in Failed", SignInFailed);
            throw new InvalidOperationException($"[{name}] Steam sign-in failed: {result.error}");
        }

        // Steam is the identity: the next Login() signs the same account straight back in.
        public override Task Logout()
        {
            PurrServices.instance.auth.Logout();
            return Task.CompletedTask;
        }
#else
        public override bool isLoggedIn => false;

        public override string playerId => null;

        public override string playerName => null;

        public override Task Login(ViewStack stack)
        {
            Debug.LogError(
                $"[{name}] Steam sign-in needs PurrServices 1.2.1 or newer and Steamworks.NET, " +
                "on a Windows, Linux or macOS standalone target. See this provider's inspector.");
            return Task.CompletedTask;
        }

        public override Task Logout() => Task.CompletedTask;
#endif
    }
}
