using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using PurrNet.Lobby;
using PurrNet.Services;
using PurrNet.UI;
using UnityEngine;

namespace PurrLobby.PurrNet
{
    public class PurrPasswordLogin : MonoView
    {
        const string KEY_PREFIX = nameof(PurrPasswordLogin) + "_";

        [SerializeField] private RectTransform _content;
        [SerializeField] private TMPro.TMP_InputField _username;
        [SerializeField] private TMPro.TMP_InputField _password;
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
                _username.text = PlayerPrefs.GetString(KEY_PREFIX + nameof(_username), "");
        }

        public void Register()
        {
            if (_rememberMe.value)
            {
                PlayerPrefs.SetString(KEY_PREFIX + nameof(_username), _username.text);
                PlayerPrefs.SetInt(KEY_PREFIX + nameof(_rememberMe), 1);
            }
            else
            {
                PlayerPrefs.DeleteKey(KEY_PREFIX + nameof(_username));
                PlayerPrefs.DeleteKey(KEY_PREFIX + nameof(_rememberMe));
            }

            HandleRegisterAsync();
        }

        public void Login()
        {
            if (_rememberMe.value)
            {
                PlayerPrefs.SetString(KEY_PREFIX + nameof(_username), _username.text);
                PlayerPrefs.SetInt(KEY_PREFIX + nameof(_rememberMe), 1);
            }
            else
            {
                PlayerPrefs.DeleteKey(KEY_PREFIX + nameof(_username));
                PlayerPrefs.DeleteKey(KEY_PREFIX + nameof(_rememberMe));
            }

            HandleLoginAsync();
        }

        private async void HandleRegisterAsync()
        {
            try
            {
                _closeParentView.canClose = false;
                _loadingOverlay.Toggle(true);

                var services = PurrServices.instance;
                var response = await services.auth.LoginAsync(_deviceId, _username.text);

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

        private async void HandleLoginAsync()
        {
            try
            {
                _closeParentView.canClose = false;
                _loadingOverlay.Toggle(true);

                var services = PurrServices.instance;
                var response = await services.auth.LoginAsync(_deviceId, _username.text);

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
