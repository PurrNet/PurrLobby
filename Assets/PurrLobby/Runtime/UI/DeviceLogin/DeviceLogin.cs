using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using PurrNet.UI;
using UnityEngine;
using PurrNet.UI.HeroUI;

namespace PurrNet.Lobby
{
    [DefaultExecutionOrder(-1000)]
    public class DeviceLogin : MonoView
    {
        const string KEY_PREFIX = nameof(DeviceLogin) + "_";
        const string WEBGL_DEVICE_ID_KEY = KEY_PREFIX + "WebGLDeviceId";

        [SerializeField] private RectTransform _content;
        [SerializeField] private TMPro.TMP_InputField _displayName;
        [SerializeField] private ToggleElement _rememberMe;
        [SerializeField] private LoadingOverlay _loadingOverlay;
        [SerializeField] private CloseParentView _closeParentView;

        private string _deviceId;
        private Action _onDone;
        private LoginCallback _onLogin;

        public delegate Task<APIResponse> LoginCallback(string deviceId, string username, bool rememberMe);

        protected override IEnumerator OnEnterTransition() => ViewTransitions.SlideFromBottom(_content);

        protected override IEnumerator OnExitTransition() => ViewTransitions.SlideToTop(_content);

        private void Awake()
        {
            _deviceId = GetDeviceId();
        }

        /// <summary>
        /// The stable device id used as the login credential. WebGL cannot use
        /// SystemInfo.deviceUniqueIdentifier because it is derived from browser and
        /// hardware data rather than a particular user. Store a random id in that
        /// browser profile instead. In the editor the id is salted with the project
        /// path (and the ParrelSync clone name) so clones get distinct accounts.
        /// </summary>
        public static string GetDeviceId()
        {
#if UNITY_EDITOR
            var salt = Application.dataPath;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-name")
                    continue;
                salt += "|" + args[i + 1];
                break;
            }

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(salt));
            return BitConverter.ToString(bytes).Replace("-", "")[..32].ToLowerInvariant();
#elif UNITY_WEBGL
            var persistedId = PlayerPrefs.GetString(WEBGL_DEVICE_ID_KEY, null);
            if (!string.IsNullOrEmpty(persistedId))
                return persistedId;

            var generatedId = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(WEBGL_DEVICE_ID_KEY, generatedId);
            PlayerPrefs.Save();
            return generatedId;
#else
            return SystemInfo.deviceUniqueIdentifier;
#endif
        }

        /// <summary>
        /// True when the user previously logged in here with "remember me" checked —
        /// the session provider can then log in silently instead of showing this view.
        /// </summary>
        public static bool TryGetRememberedLogin(out string deviceId, out string displayName)
        {
            if (PlayerPrefs.HasKey(KEY_PREFIX + nameof(_rememberMe)))
            {
                deviceId = GetDeviceId();
                displayName = PlayerPrefs.GetString(KEY_PREFIX + nameof(_displayName), "");
                return true;
            }

            deviceId = null;
            displayName = null;
            return false;
        }

        /// <summary>Forgets the remembered login so an explicit logout doesn't silently log back in.</summary>
        public static void ClearRememberedLogin()
        {
            PlayerPrefs.DeleteKey(KEY_PREFIX + nameof(_displayName));
            PlayerPrefs.DeleteKey(KEY_PREFIX + nameof(_rememberMe));
        }

        public void Setup(Action onDone, LoginCallback loginCallback)
        {
            _onDone = onDone;
            _onLogin = loginCallback;
            // This is a required startup step. Dismissing it leaves the session
            // provider awaiting a login that can no longer be completed.
            if (_closeParentView)
                _closeParentView.canClose = false;

            _rememberMe.value = PlayerPrefs.HasKey(KEY_PREFIX + nameof(_rememberMe));
            if (_rememberMe.value)
                _displayName.text = PlayerPrefs.GetString(KEY_PREFIX + nameof(_displayName), "");
        }

        private void Update()
        {
            // Consume back before generic window handlers can dismiss this
            // mandatory view. Successful login still closes it explicitly.
            if (isTopMost && BackInput.WasBackPressed())
                BackInput.TryConsume();
        }

        public void Login()
        {
            if (_rememberMe.value)
            {
                PlayerPrefs.SetString(KEY_PREFIX + nameof(_displayName), _displayName.text);
                PlayerPrefs.SetInt(KEY_PREFIX + nameof(_rememberMe), 1);
            }
            else
            {
                PlayerPrefs.DeleteKey(KEY_PREFIX + nameof(_displayName));
                PlayerPrefs.DeleteKey(KEY_PREFIX + nameof(_rememberMe));
            }

            UiTask.Run(HandleLoginAsync, "Login Failed", _loadingOverlay);
        }

        private async Task HandleLoginAsync()
        {
            var response = await _onLogin(_deviceId, _displayName.text, _rememberMe.value);

            if (!this)
                return;

            if (!response.success)
            {
                Toaster.Push("Login Failed", response.error, true);
                return;
            }

            _onDone?.Invoke();
            CloseMe();
        }
    }
}
