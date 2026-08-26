using System.Threading.Tasks;
using PurrNet.UI;
using UnityEngine;
#if NAKAMA
using System;
#else
// Serialized fields stay declared so preset assets keep their values without the SDK.
#pragma warning disable CS0414
#endif

namespace PurrNet.Lobby.Nakama
{
    [ProviderDependency("com.heroiclabs.nakama-unity", "Nakama Unity")]
    [CreateAssetMenu(menuName = "PurrLobby/Nakama/Session Provider", order = -202)]
    public class NakamaSessionProvider : SessionProvider
    {
        [SerializeField] private NakamaConfig _config;

        [Tooltip("PlayerPrefs key used to cache the auth + refresh tokens when the user opts into 'Remember Me' on the device login view. Leave empty to disable persistence entirely. In the editor the project folder name is appended to the key so MPPM/clone instances do not share a session.")]
        [SerializeField] private string _sessionPlayerPrefKey = "purr_lobby.nakama.session";

        public NakamaConfig config => _config;

#if NAKAMA
        private string scopedPlayerPrefKey
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

        private TaskCompletionSource<bool> _loggingIn;
        private Task _loggingOut;

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

            // The persisted tokens are gone or expired, but the user asked to be
            // remembered: the device id is the credential, so log in silently
            // instead of prompting again.
            if (DeviceLogin.TryGetRememberedLogin(out var deviceId, out var rememberedName))
            {
                var silent = await DoLogin(deviceId, rememberedName, rememberMe: true);

                if (silent.success)
                    return;

                Debug.LogWarning($"[{name}] Silent device login failed: {silent.error}");
            }

            _loggingIn = new TaskCompletionSource<bool>();

            var deviceLogin = stack.Push<DeviceLogin>();
            deviceLogin.Setup(SetFlag, DoLogin);

            await _loggingIn.Task;
        }

        public override Task Logout()
        {
            if (_loggingOut != null && !_loggingOut.IsCompleted)
                return _loggingOut;

            _loggingOut = LogoutAsync();
            return _loggingOut;
        }

        private async Task LogoutAsync()
        {
            try
            {
                // Forget the remembered device login too, otherwise the next Login()
                // silently signs straight back in and logout appears to do nothing.
                DeviceLogin.ClearRememberedLogin();
                ClearPersistedSession();
                await NakamaConnection.instance.LogoutAsync();
            }
            finally
            {
                _loggingOut = null;
            }
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

                // Device auth only applies the username when the account is created;
                // an existing account keeps its old name. Nakama supports renaming,
                // so honor what the user actually typed.
                if (!string.IsNullOrEmpty(username) && conn.username != username)
                    await TryRenameAsync(conn, deviceId, username);

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

        private static async Task TryRenameAsync(NakamaConnection conn, string deviceId, string username)
        {
            try
            {
                await conn.client.UpdateAccountAsync(conn.session, username);

                // The session token still carries the old username; re-authenticate
                // so playerName reflects the new one immediately.
                await conn.AuthenticateDeviceAsync(deviceId, username);
            }
            catch (Exception ex)
            {
                // Usernames are unique in Nakama — the pick may be taken.
                Toaster.Push("Welcome back",
                    $"Couldn't rename to `{username}` — logged in as {conn.username}. ({ex.Message})");
            }
        }

        private bool TryRestorePersistedSession(NakamaConnection conn)
        {
            var key = scopedPlayerPrefKey;
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
            var key = scopedPlayerPrefKey;
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(conn.authToken))
                return;
            PlayerPrefs.SetString(key, $"{conn.authToken}|{conn.refreshToken}");
            PlayerPrefs.Save();
        }

        private void ClearPersistedSession()
        {
            var key = scopedPlayerPrefKey;
            if (string.IsNullOrEmpty(key) || !PlayerPrefs.HasKey(key))
                return;
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
#else
        private const string NakamaUnavailable =
            "Nakama Unity is not installed. Install it from the LobbyManager inspector or the PurrNet Packages window.";

        public override bool isLoggedIn => false;

        public override string playerId => null;

        public override string playerName => null;

        public override Task Login(ViewStack stack)
        {
            Debug.LogError($"[{name}] {NakamaUnavailable}", this);
            return Task.CompletedTask;
        }

        public override Task Logout() => Task.CompletedTask;
#endif
    }
}
