using System.Collections;
using PurrNet.Lobby;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class MainMenuView : MonoView
    {
        [SerializeField] private RectTransform _content;
        [SerializeField] private GameObject _matchmakingSection;
        [SerializeField] private GameObject _lobbySection;
        [SerializeField] private TMPro.TMP_Text _profileDisplayName;

        private LobbyManager _manager;
        private MenuOrchestrator _orchestrator;

        protected override IEnumerator OnEnterTransition() => ViewTransitions.FadeIn(this);

        protected override IEnumerator OnExitTransition() => ViewTransitions.FadeOut(this);

        protected override IEnumerator OnCulledTransition() => ViewTransitions.SlideToLeft(_content);

        protected override IEnumerator OnUnculledTransition() => ViewTransitions.SlideFromLeft(_content);

        public void Setup(LobbyManager manager, MenuOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
            _manager = manager;

            if (_matchmakingSection)
                _matchmakingSection.SetActive(orchestrator.matchmakingProvider);

            if (_lobbySection)
                _lobbySection.SetActive(orchestrator.lobbyProvider);

            var username = orchestrator.sessionProvider ?
                orchestrator.sessionProvider.playerName : "Guest";

            _profileDisplayName.text = $"<icon=account_outline> {username}";
        }

        public void Matchmake()
        {

        }

        public void Logout()
        {
            if (_orchestrator.lobbyProvider)
                _orchestrator.lobbyProvider.Logout();

            if (_orchestrator.matchmakingProvider)
                _orchestrator.matchmakingProvider.Logout();

            if (_orchestrator.gameStarterProvider)
                _orchestrator.gameStarterProvider.Logout();

            CloseMe();

            // restart loop
            _manager.Initialize();
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
