using System.Threading.Tasks;
using PurrNet.Services;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby.PurrNet
{
    [CreateAssetMenu(menuName = "PurrLobby/PurrNet/Session Provider", order = -201)]
    public class PurrNetSessionProvider : SessionProvider
    {
        public override bool isLoggedIn => PurrServices.instance.auth.isAuthenticated;

        public override string playerId => PurrServices.instance.auth.playerId;

        public override string playerName => PurrServices.instance.auth.displayName;

        private TaskCompletionSource<bool> _loggingIn;

        public override async Task Login(ViewStack stack)
        {
            var services = PurrServices.instance;

            if (services.auth.isAuthenticated)
            {
                var result = await services.auth.ValidateSessionAsync();

                if (result.success)
                    return;

                Debug.LogWarning(result.error);
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

        static async Task<APIResponse> Login(string deviceId, string displayName, bool rememberMe)
        {
            var services = PurrServices.instance;
            var result = await services.auth.LoginAsync(deviceId, displayName);
            return result.success ? APIResponse.Success() : APIResponse.Failure(result.error);
        }

        public override Task Logout()
        {
            var services = PurrServices.instance;
            services.auth.Logout();
            return Task.CompletedTask;
        }
    }
}
