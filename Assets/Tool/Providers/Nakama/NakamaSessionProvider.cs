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

        [Tooltip("PlayerPrefs key used to cache the auth + refresh tokens when the user opts into 'Remember Me' on the device login view. Leave empty to disable persistence entirely. In the editor the project folder name is appended to the key so MPPM/clone instances do not share a session.")]
        [SerializeField] private string _sessionPlayerPrefKey = "purr_lobby.nakama.session";

        private string ScopedPlayerPrefKey
        {
            get
            {
                if (string.IsNullOrEmpty(_sessionPlayerPrefKey))
                    return _sessionPlayerPrefKey;
#if UNITY_EDITOR
                var projectPath = Application.dataPath;
                var folderName = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(projectPath));
                return _sessionPlayerPrefKey + "_" + folderName;
#else
                return _sessionPlayerPrefKey;
#endif
            }
        }

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
            var key = ScopedPlayerPrefKey;
            if (string.IsNullOrEmpty(key) || !PlayerPrefs.HasKey(key))
                return false;

            var combined = PlayerPrefs.GetString(key);
            var parts = combined.Split('|');
            if (parts.Length < 1 || string.IsNullOrEmpty(parts[0]))
                return false;

            return conn.TryRestoreFromTokens(parts[0], parts.Length > 1 ? parts[1] : null);
        }

        private void PersistSession(NakamaConnection conn)
        {
            var key = ScopedPlayerPrefKey;
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(conn.authToken))
                return;
            PlayerPrefs.SetString(key, $"{conn.authToken}|{conn.refreshToken}");
            PlayerPrefs.Save();
        }

        private void ClearPersistedSession()
        {
            var key = ScopedPlayerPrefKey;
            if (string.IsNullOrEmpty(key) || !PlayerPrefs.HasKey(key))
                return;
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
#endif
