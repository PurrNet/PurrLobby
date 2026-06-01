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
    public class DeviceLogin : MonoView
    {
        const string KEY_PREFIX = nameof(DeviceLogin) + "_";

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
            _deviceId = BitConverter.ToString(bytes).Replace("-", "")[..32].ToLowerInvariant();
#else
            _deviceId = SystemInfo.deviceUniqueIdentifier;
#endif
        }

        public void Setup(Action onDone, LoginCallback loginCallback)
        {
            _onDone = onDone;
            _onLogin = loginCallback;
            _rememberMe.value = PlayerPrefs.HasKey(KEY_PREFIX + nameof(_rememberMe));
            if (_rememberMe.value)
                _displayName.text = PlayerPrefs.GetString(KEY_PREFIX + nameof(_displayName), "");
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

            HandleLoginAsync();
        }

        private async void HandleLoginAsync()
        {
            try
            {
                _closeParentView.canClose = false;
                _loadingOverlay.Toggle(true);

                var response = await _onLogin(_deviceId, _displayName.text, _rememberMe.value);

                if (!response.success)
                {
                    Toaster.Push("Login Failed", response.error, true);
                    return;
                }

                _onDone?.Invoke();
                CloseMe();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                _closeParentView.canClose = true;
                _loadingOverlay.Toggle(false);
            }
        }
    }
}
