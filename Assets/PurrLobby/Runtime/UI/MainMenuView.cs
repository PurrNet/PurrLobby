using System;
using System.Collections;
using System.Threading.Tasks;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class MainMenuView : MonoView
    {
        [SerializeField] private RectTransform _content;
        [SerializeField] private GameObject _matchmakingSection;
        [SerializeField] private GameObject _lobbySection;
        [SerializeField] private GameObject _createLobbyButton;
        [SerializeField] private GameObject _joinWithCodeButton;
        [SerializeField] private GameObject _browseLobbiesButton;
        [SerializeField] private TMPro.TMP_Text _profileDisplayName;

        private LobbyManager _manager;
        private GameOrchestrator _orchestrator;
        private bool _loggingOut;

        protected override IEnumerator OnEnterTransition() => ViewTransitions.FadeIn(this);

        protected override IEnumerator OnExitTransition() => ViewTransitions.FadeOut(this);

        protected override IEnumerator OnCulledTransition() => ViewTransitions.SlideToLeft(_content);

        protected override IEnumerator OnUnculledTransition() => ViewTransitions.SlideFromLeft(_content);

        public void Setup(LobbyManager manager, GameOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
            _manager = manager;

            if (_matchmakingSection)
                _matchmakingSection.SetActive(orchestrator.matchmakingProvider);

            var lobbyProvider = orchestrator.lobbyProvider;
            if (_lobbySection)
                _lobbySection.SetActive(lobbyProvider);

            var caps = lobbyProvider ? lobbyProvider.capabilities : LobbyCapabilities.None;
            if (_createLobbyButton)
                _createLobbyButton.SetActive(caps.Has(LobbyCapabilities.CreateLobby));
            if (_joinWithCodeButton)
                _joinWithCodeButton.SetActive(caps.Has(LobbyCapabilities.JoinLobbyByCode));
            if (_browseLobbiesButton)
                _browseLobbiesButton.SetActive(caps.Has(LobbyCapabilities.QueryLobbies));

            var username = orchestrator.sessionProvider ?
                orchestrator.sessionProvider.playerName : "Guest";

            _profileDisplayName.text = $"<icon=account_outline> {username}";

            ReportLastExit();
        }

        private void ReportLastExit()
        {
            switch (_orchestrator.lastExitReason)
            {
                case GameExitReason.GameOver:
                    Toaster.Push("Game Over", "The match has ended.");
                    break;
                case GameExitReason.ConnectionLost:
                    Toaster.PushError("Disconnected", "Lost connection to the game.");
                    break;
            }

            _orchestrator.lastExitReason = GameExitReason.None;
        }

        public void Matchmake()
        {
            parentStack.Push<MatchmakingView>().Setup(_orchestrator, new MatchmakingRequest
            {
                gameMode = "PurrNet"
            });
        }

        public void JoinWithCode()
        {
            parentStack.Push<JoinWithCodeView>().Setup(_orchestrator);
        }

        public void BrowseLobbies()
        {
            parentStack.Push<LobbyBrowserView>().Setup(_orchestrator);
        }

        public void Logout()
        {
            LogoutAsync().Forget("[MainMenuView] Logout failed");
        }

        private async Task LogoutAsync()
        {
            if (_loggingOut)
                return;

            _loggingOut = true;

            try
            {
                if (_orchestrator.matchmakingProvider)
                    await _orchestrator.matchmakingProvider.Logout();

                if (_orchestrator.gameAllocator)
                    await _orchestrator.gameAllocator.Logout();

                if (_orchestrator.lobbyProvider)
                    await _orchestrator.lobbyProvider.Logout();

                if (_orchestrator.sessionProvider)
                    await _orchestrator.sessionProvider.Logout();

                CloseMe();
                await _manager.InitializeAsync();
            }
            catch (Exception e)
            {
                _loggingOut = false;
                Debug.LogException(e);
                Toaster.PushError("Failed to log out", e);
            }
        }

        public void CreateLobby()
        {
            var view = parentStack.Push<CreateLobbyView>();
            view.Setup(_orchestrator);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
