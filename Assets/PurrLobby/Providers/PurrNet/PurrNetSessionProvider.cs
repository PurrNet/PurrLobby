using System;
using System.Threading.Tasks;
using PurrNet.UI;
using UnityEngine;
#if PURR_SERVICES
using PurrNet.Services;
#endif

namespace PurrNet.Lobby.PurrNet
{
    [ProviderDependency("dev.purrnet.services", "PurrServices")]
    [CreateAssetMenu(menuName = "PurrLobby/PurrNet/Session Provider", order = -201)]
    public class PurrNetSessionProvider : SessionProvider
    {
#if PURR_SERVICES
        public override bool isLoggedIn => PurrServices.instance.auth.isAuthenticated;

        public override string playerId => PurrServices.instance.auth.playerId;

        public override string playerName => PurrServices.instance.auth.displayName;

        private TaskCompletionSource<bool> _loggingIn;

        public override async Task Login(ViewStack stack)
        {
            var services = PurrServices.instance;
            if (!PurrServicesConfiguration.IsConfigured(services))
            {
                Toaster.PushError("Online Services Unavailable", PurrServicesConfiguration.UserError);
                throw new InvalidOperationException(PurrServicesConfiguration.DeveloperError);
            }

            if (services.auth.isAuthenticated)
            {
                var result = await services.auth.ValidateSessionAsync();

                if (result.success)
                    return;

                Debug.LogWarning(result.error);
            }

            // The session expired but the user asked to be remembered: the device id
            // is the credential, so log back in silently instead of prompting again.
            if (DeviceLogin.TryGetRememberedLogin(out var deviceId, out var rememberedName))
            {
                var result = await services.auth.LoginAsync(deviceId, rememberedName);

                if (result.success)
                    return;

                Debug.LogWarning($"[{name}] Silent device login failed: {result.error}");
            }

            _loggingIn = new TaskCompletionSource<bool>();

            var deviceLogin = stack.Push<DeviceLogin>();
            deviceLogin.Setup(SetFlag, Login);

            await _loggingIn.Task;
        }

        void SetFlag()
        {
            _loggingIn.TrySetResult(true);
        }

        async Task<APIResponse> Login(string deviceId, string displayName, bool rememberMe)
        {
            var services = PurrServices.instance;
            if (!PurrServicesConfiguration.IsConfigured(services))
            {
                PurrServicesConfiguration.LogDeveloperError(this);
                return APIResponse.Failure(PurrServicesConfiguration.UserError);
            }

            var result = await services.auth.LoginAsync(deviceId, displayName);

            if (!result.success)
                return APIResponse.Failure(result.error);

            // The device already had an account: the server keeps its original display
            // name and ignores the typed one. Say so instead of silently swapping.
            var actualName = services.auth.displayName;
            if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(actualName) && actualName != displayName)
                Toaster.Push("Welcome back", $"This device already has an account — logged in as {actualName}.");

            return APIResponse.Success();
        }

        public override Task Logout()
        {
            // Forget the remembered device login too, otherwise the next Login()
            // silently signs straight back in and logout appears to do nothing.
            DeviceLogin.ClearRememberedLogin();

            var services = PurrServices.instance;
            services.auth.Logout();
            return Task.CompletedTask;
        }
#else
        public override bool isLoggedIn => false;

        public override string playerId => null;

        public override string playerName => null;

        public override Task Login(ViewStack stack)
        {
            Debug.LogError($"[{name}] PurrServices is not installed. Install it from this provider's inspector.");
            return Task.CompletedTask;
        }

        public override Task Logout() => Task.CompletedTask;
#endif
    }
}
