using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using PurrNet.Lobby;
using PurrNet.UI;
using PurrNet.Services;
using UnityEngine;

namespace PurrNet.Lobby.PurrNet
{
    public class PurrDeviceLogin : MonoView
    {
        const string KEY_PREFIX = nameof(PurrDeviceLogin) + "_";

        [SerializeField] private RectTransform _content;
        [SerializeField] private TMPro.TMP_InputField _displayName;
        [SerializeField] private ToggleElement _rememberMe;
        [SerializeField] private LoadingOverlay _loadingOverlay;
        [SerializeField] private CloseParentView _closeParentView;

        private string _deviceId;
        private Action _onDone;

        protected override IEnumerator OnEnterTransition() => ViewTransitions.SlideFromBottom(_content);

        protected override IEnumerator OnExitTransition() => ViewTransitions.SlideToTop(_content);

        private void Awake()
        {
#if UNITY_EDITOR
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Application.dataPath));
            _deviceId = BitConverter.ToString(bytes).Replace("-", "")[..32].ToLowerInvariant();
#else
            _deviceId = SystemInfo.deviceUniqueIdentifier;
#endif
        }

        public void Setup(Action onDone)
        {
            _onDone = onDone;
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

                var services = PurrServices.instance;
                var response = await services.auth.LoginAsync(_deviceId, _displayName.text);

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
                _closeParentView.canClose = false;
                _loadingOverlay.Toggle(false);
            }
        }
    }
}
