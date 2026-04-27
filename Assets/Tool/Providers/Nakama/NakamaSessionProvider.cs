#if NAKAMA
using System;
using System.Threading.Tasks;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby.Nakama
{
    [CreateAssetMenu(menuName = "PurrLobby/Nakama/Session Provider", order = -202)]
    public class NakamaSessionProvider : SessionProvider
    {
        [SerializeField] private NakamaConfig _config;

        [Tooltip("PlayerPrefs key used to cache the auth + refresh tokens when the user opts into 'Remember Me' on the device login view. Leave empty to disable persistence entirely.")]
        [SerializeField] private string _sessionPlayerPrefKey = "purr_lobby.nakama.session";

        public override bool isLoggedIn => NakamaConnection.instance.isAuthenticated;

        public override string playerId => NakamaConnection.instance.userId;

        public override string playerName => NakamaConnection.instance.username;

        public NakamaConfig config => _config;

        private TaskCompletionSource<bool> _loggingIn;

        public override async Task Login(ViewStack stack)
        {
            if (_config == null)
            {
                Debug.LogError($"[{name}] NakamaConfig is not assigned.");
                return;
            }

            var conn = NakamaConnection.instance;
            conn.EnsureClient(_config);

            // Reuse a still-valid session (in-memory or persisted on disk) silently — same UX as
            // PurrNet when ValidateSessionAsync succeeds.
            if (conn.isAuthenticated || TryRestorePersistedSession(conn))
            {
                await conn.EnsureSocketAsync();
                return;
            }

            _loggingIn = new TaskCompletionSource<bool>();

            var deviceLogin = stack.Push<DeviceLogin>();
            deviceLogin.Setup(SetFlag, DoLogin);

            await _loggingIn.Task;
        }

        public override async Task Logout()
        {
            ClearPersistedSession();
            await NakamaConnection.instance.LogoutAsync();
        }

        private void SetFlag()
        {
            _loggingIn?.TrySetResult(true);
        }

        private async Task<APIResponse> DoLogin(string deviceId, string username, bool rememberMe)
        {
            try
            {
                var conn = NakamaConnection.instance;
                await conn.AuthenticateDeviceAsync(deviceId, username);
                await conn.EnsureSocketAsync();

                if (rememberMe)
                    PersistSession(conn);
                else
                    ClearPersistedSession();

                return APIResponse.Success();
            }
            catch (Exception ex)
            {
                return APIResponse.Failure(ex.Message);
            }
        }

        private bool TryRestorePersistedSession(NakamaConnection conn)
        {
            if (string.IsNullOrEmpty(_sessionPlayerPrefKey) || !PlayerPrefs.HasKey(_sessionPlayerPrefKey))
                return false;

            var combined = PlayerPrefs.GetString(_sessionPlayerPrefKey);
            var parts = combined.Split('|');
            if (parts.Length < 1 || string.IsNullOrEmpty(parts[0]))
                return false;

            return conn.TryRestoreFromTokens(parts[0], parts.Length > 1 ? parts[1] : null);
        }

        private void PersistSession(NakamaConnection conn)
        {
            if (string.IsNullOrEmpty(_sessionPlayerPrefKey) || string.IsNullOrEmpty(conn.authToken))
                return;
            PlayerPrefs.SetString(_sessionPlayerPrefKey, $"{conn.authToken}|{conn.refreshToken}");
            PlayerPrefs.Save();
        }

        private void ClearPersistedSession()
        {
            if (string.IsNullOrEmpty(_sessionPlayerPrefKey) || !PlayerPrefs.HasKey(_sessionPlayerPrefKey))
                return;
            PlayerPrefs.DeleteKey(_sessionPlayerPrefKey);
            PlayerPrefs.Save();
        }
    }
}
#endif
