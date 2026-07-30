using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using PurrNet.UI.HeroUI;

namespace PurrNet.Lobby
{
    public class JoinWithCodeView : SlidePageView
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private LoadingOverlay _loadingOverlay;
        [SerializeField] private TMP_InputField _codeField;

        private GameOrchestrator _orchestrator;

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

        public void Submit()
        {
            UiTask.Run(SubmitAsync, "Join Failed", _loadingOverlay, disableGroup: _canvasGroup);
        }

        private async Task SubmitAsync()
        {
            var response = await _orchestrator.lobbyProvider.JoinLobbyByCode(_codeField.text.Trim());

            if (!this)
                return;

            if (!response.success)
            {
                Toaster.Push("Join Failed", response.error, true);
                return;
            }

            successfulExit = true;
            parentStack.ReplaceOrPush<LobbyView>(this).Setup(response.lobby, _orchestrator);
        }
    }
}
