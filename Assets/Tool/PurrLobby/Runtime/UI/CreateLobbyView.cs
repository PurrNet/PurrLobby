using System.Threading.Tasks;
using PurrNet.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PurrNet.UI.HeroUI;

namespace PurrNet.Lobby
{
    public class CreateLobbyView : SlidePageView
    {
        [SerializeField] TMP_InputField _lobbyName;
        [SerializeField] ToggleElement _publicToggle;
        [SerializeField] Slider _playerCountSlider;
        [SerializeField] TMP_Text _playerCountText;
        [Space]
        [SerializeField] private LoadingOverlay _loadingOverlay;
        [SerializeField] private CloseParentView _closeParentView;

        private GameOrchestrator _orchestrator;

        public override void OnPopped()
        {
            _playerCountSlider.onValueChanged.RemoveAllListeners();
        }

        public void Setup(GameOrchestrator provider)
        {
            _orchestrator = provider;
            successfulExit = false;
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

        public void CreateLobby()
        {
            UiTask.Run(CreateLobbyAsync, "Lobby Creation Failed", _loadingOverlay, closeGuard: _closeParentView);
        }

        private async Task CreateLobbyAsync()
        {
            var lobbySettings = new LobbySettings
            {
                maxPlayers = Mathf.RoundToInt(_playerCountSlider.value),
                visibility = _publicToggle.value ? LobbyVisibility.Public : LobbyVisibility.Private,
                name = _lobbyName.text
            };

            var response = await _orchestrator.lobbyProvider.CreateLobby(lobbySettings);

            if (!this)
                return;

            if (!response.success)
            {
                Toaster.Push("Lobby Creation Failed", response.error, true);
                return;
            }

            successfulExit = true;
            var lobbyView = parentStack.ReplaceOrPush<LobbyView>(this);
            lobbyView.Setup(response.lobby, _orchestrator);
        }
    }
}
