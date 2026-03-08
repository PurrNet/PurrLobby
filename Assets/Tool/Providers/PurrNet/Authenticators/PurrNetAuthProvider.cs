using System.Threading.Tasks;
using PurrNet.Services;
using PurrNet.UI;
using UnityEngine;

namespace PurrLobby.PurrNet
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

            var deviceLogin = stack.Push<PurrDeviceLogin>();
            deviceLogin.Setup(() => _loggingIn.TrySetResult(true));

            await _loggingIn.Task;
        }

        public override Task Logout()
        {
            var services = PurrServices.instance;
            services.auth.Logout();
            return Task.CompletedTask;
        }
    }
}
