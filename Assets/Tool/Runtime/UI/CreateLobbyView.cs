using System;
using System.Collections;
using PurrLobby;
using PurrNet.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PurrNet.Lobby
{
    public class CreateLobbyView : MonoView
    {
        [SerializeField] private RectTransform _content;
        [Space]
        [SerializeField] TMP_InputField _lobbyName;
        [SerializeField] ToggleElement _publicToggle;
        [SerializeField] Slider _playerCountSlider;
        [SerializeField] TMP_Text _playerCountText;
        [Space]
        [SerializeField] private LoadingOverlay _loadingOverlay;
        [SerializeField] private CloseParentView _closeParentView;

        private MenuOrchestrator _orchestrator;

        public override void OnPopped()
        {
            _playerCountSlider.onValueChanged.RemoveAllListeners();
        }

        public void Setup(MenuOrchestrator provider)
        {
            _orchestrator = provider;
            _successfulExit = false;
            _playerCountSlider.minValue = 1;
            _playerCountSlider.maxValue = _orchestrator.lobbyProvider.maxPlayer;
            _playerCountSlider.value = _orchestrator.lobbyProvider.maxPlayer;
            OnPlayerCountChanged(_orchestrator.lobbyProvider.maxPlayer);

            _playerCountSlider.onValueChanged.AddListener(OnPlayerCountChanged);
        }

        private void OnPlayerCountChanged(float newVal)
        {
            int val = Mathf.RoundToInt(newVal);
            _playerCountText.text = val.ToString();
        }

        private bool _successfulExit = false;

        public async void CreateLobby()
        {
            try
            {
                _closeParentView.canClose = false;
                _loadingOverlay.Toggle(true);

                var lobbySettings = new LobbySettings
                {
                    maxPlayers = Mathf.RoundToInt(_playerCountSlider.value),
                    visibility = _publicToggle.value ? LobbyVisibility.Public : LobbyVisibility.Private,
                    name = _lobbyName.text
                };

                var response = await _orchestrator.lobbyProvider.CreateLobby(lobbySettings);

                if (!response.success)
                {
                    Toaster.Push("Lobby Creation Failed", response.error, true);
                    return;
                }

                _successfulExit = true;
                var lobbyView = parentStack.Replace<LobbyView>();
                lobbyView.Setup(response.lobby);
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

        protected override IEnumerator OnEnterTransition()
        {
            var fade = ViewTransitions.FadeIn(this);
            var slide = ViewTransitions.SlideFromRight(_content);
            return ViewTransitions.Parallel(fade, slide);
        }

        protected override IEnumerator OnExitTransition()
        {
            if (_successfulExit)
            {
                var fade = ViewTransitions.FadeOut(this);
                var slide = ViewTransitions.SlideToLeft(_content);
                return ViewTransitions.Parallel(fade, slide);
            }
            else
            {
                var fade = ViewTransitions.FadeOut(this);
                var slide = ViewTransitions.SlideToRight(_content);
                return ViewTransitions.Parallel(fade, slide);
            }
        }
    }
}
