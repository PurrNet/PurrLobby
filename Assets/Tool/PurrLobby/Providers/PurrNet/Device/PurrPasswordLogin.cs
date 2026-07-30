#if PURR_SERVICES
using System;
using System.Collections;
using System.Threading.Tasks;
using PurrNet.Services;
using PurrNet.UI;
using UnityEngine;
using PurrNet.UI.HeroUI;

namespace PurrNet.Lobby.PurrNet
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

        private Action _onDone;
        private bool _busy;

        protected override IEnumerator OnEnterTransition() => ViewTransitions.SlideFromBottom(_content);

        protected override IEnumerator OnExitTransition() => ViewTransitions.SlideToTop(_content);

        public void Setup(Action onDone)
        {
            _onDone = onDone;
            _rememberMe.value = PlayerPrefs.HasKey(KEY_PREFIX + nameof(_rememberMe));
            if (_rememberMe.value)
                _username.text = PlayerPrefs.GetString(KEY_PREFIX + nameof(_username), "");
        }

        public void Register()
        {
            var auth = PurrServices.instance.auth;
            HandleAuthAsync(() => auth.RegisterAsync(_username.text, _password.text, _username.text), "Registration Failed");
        }

        public void Login()
        {
            var auth = PurrServices.instance.auth;
            HandleAuthAsync(() => auth.LoginWithPasswordAsync(_username.text, _password.text), "Login Failed");
        }

        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(_username.text))
            {
                Toaster.Push("Missing Username", "Enter a username first.", true);
                return false;
            }

            if (string.IsNullOrEmpty(_password.text))
            {
                Toaster.Push("Missing Password", "Enter a password first.", true);
                return false;
            }

            return true;
        }

        private void SaveRememberMe()
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
        }

        private async void HandleAuthAsync(Func<Task<AuthResult>> authenticate, string failureTitle)
        {
            if (_busy || !ValidateInputs())
                return;

            SaveRememberMe();

            try
            {
                _busy = true;
                _closeParentView.canClose = false;
                _loadingOverlay.Toggle(true);

                var response = await authenticate();

                if (!response.success)
                {
                    Toaster.Push(failureTitle, response.error, true);
                    return;
                }

                _onDone?.Invoke();
                CloseMe();
            }
            catch (Exception e)
            {
                Toaster.Push(failureTitle, e.Message, true);
                Debug.LogException(e);
            }
            finally
            {
                _busy = false;
                _closeParentView.canClose = true;
                _loadingOverlay.Toggle(false);
            }
        }
    }
}
#endif
