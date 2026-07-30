using System;
using System.Threading.Tasks;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class LobbyManager : MonoBehaviour
    {
        [SerializeField] private GameOrchestrator _orchestrator;
        [SerializeField] private ViewStack _stack;

        private LobbyProvider _subscribedProvider;

        private void Start()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            UnsubscribeExternalJoin();
        }

        public void Initialize()
        {
            InitializeAsync().Forget("[LobbyManager] Initialize failed");
        }

        public async Task InitializeAsync()
        {
            GameOrchestrator.active = _orchestrator;

            if (_orchestrator.sessionProvider)
                await _orchestrator.sessionProvider.Login(_stack);

            if (_orchestrator.lobbyProvider)
                await _orchestrator.lobbyProvider.Initialize();

            if (_orchestrator.matchmakingProvider)
                await _orchestrator.matchmakingProvider.Initialize();

            if (_orchestrator.gameAllocator)
                await _orchestrator.gameAllocator.Initialize();

            SubscribeExternalJoin();

            _stack.Push<MainMenuView>().Setup(this, _orchestrator);
        }

        private void SubscribeExternalJoin()
        {
            UnsubscribeExternalJoin();

            if (_orchestrator.lobbyProvider)
            {
                _subscribedProvider = _orchestrator.lobbyProvider;
                _subscribedProvider.onExternalJoinRequested += OnExternalJoinRequested;
            }
        }

        private void UnsubscribeExternalJoin()
        {
            if (_subscribedProvider)
                _subscribedProvider.onExternalJoinRequested -= OnExternalJoinRequested;
            _subscribedProvider = null;
        }

        private void OnExternalJoinRequested(string lobbyId)
        {
            JoinExternalAsync(lobbyId).Forget("[LobbyManager] External join failed");
        }

        /// <summary>
        /// Handles a platform join request (e.g. an accepted Steam overlay invite):
        /// leaves the current lobby if any, joins the requested one behind a loading
        /// view, and opens the lobby screen.
        /// </summary>
        private async Task JoinExternalAsync(string lobbyId)
        {
            var provider = _orchestrator.lobbyProvider;
            if (!provider || string.IsNullOrEmpty(lobbyId))
                return;

            if (!provider.capabilities.Has(LobbyCapabilities.JoinLobbyById))
                return;

            if (_orchestrator.activeLobby?.id == lobbyId)
                return;

            // Leave the current lobby through the standard path (pops its view too).
            var currentLobbyView = _stack.GetFirstView<LobbyView>();
            if (currentLobbyView)
                currentLobbyView.LeaveLobby();

            var loadingView = _stack.Push<LoadingView>();
            loadingView.Setup("Joining lobby ...");

            try
            {
                var response = await provider.JoinLobby(lobbyId);

                if (!this)
                    return;

                if (!response.success)
                {
                    Toaster.PushError("Failed to join lobby", response.error);
                    return;
                }

                _stack.Push<LobbyView>().Setup(response.lobby, _orchestrator);
            }
            catch (Exception e)
            {
                Toaster.PushError("Failed to join lobby", e);
                Debug.LogException(e);
            }
            finally
            {
                if (loadingView)
                    loadingView.PopMe();
            }
        }
    }
}
