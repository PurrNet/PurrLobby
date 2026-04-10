using System;
using System.Collections;
using PurrNet.UI;
using TMPro;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class JoinWithCodeView : MonoView
    {
        [SerializeField] private RectTransform _content;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private LoadingOverlay _loadingOverlay;
        [SerializeField] private TMP_InputField _codeField;

        private GameOrchestrator _orchestrator;
        private bool _successfulExit;

        public void Setup(GameOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
            _codeField.onSubmit.RemoveAllListeners();
            _codeField.text = "";
            _codeField.ActivateInputField();
            _codeField.onSubmit.AddListener(OnSubmit);
        }

        private void OnSubmit(string text)
        {
            Submit();
        }

        public async void Submit()
        {
            try
            {
                _loadingOverlay.Toggle(true);
                _canvasGroup.interactable = false;

                var response = await _orchestrator.lobbyProvider.JoinLobbyByCode(_codeField.text);
                if (!response.success)
                {
                    Toaster.Push("Join Failed", response.error, true);
                    return;
                }

                _successfulExit = true;
                parentStack.ReplaceOrPush<LobbyView>(this).Setup(response.lobby, _orchestrator);
            }
            catch (Exception e)
            {
                Toaster.Push("Join Failed", e.Message, true);
            }
            finally
            {
                _loadingOverlay.Toggle(false);
                _canvasGroup.interactable = true;
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
