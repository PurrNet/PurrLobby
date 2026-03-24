using System;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class LobbyManager : MonoBehaviour
    {
        [SerializeField] private MenuOrchestrator _orchestrator;
        [SerializeField] private ViewStack _stack;

        private void Start()
        {
            Initialize();
        }

        public async void Initialize()
        {
            try
            {
                if (_orchestrator.sessionProvider)
                    await _orchestrator.sessionProvider.Login(_stack);

                if (_orchestrator.lobbyProvider)
                    await _orchestrator.lobbyProvider.Login(_stack);

                if (_orchestrator.matchmakingProvider)
                    await _orchestrator.matchmakingProvider.Login(_stack);

                _stack.Push<MainMenuView>().Setup(this, _orchestrator);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
