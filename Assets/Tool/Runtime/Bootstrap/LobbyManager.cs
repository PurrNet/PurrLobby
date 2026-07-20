using System.Threading.Tasks;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class LobbyManager : MonoBehaviour
    {
        [SerializeField] private GameOrchestrator _orchestrator;
        [SerializeField] private ViewStack _stack;

        private void Start()
        {
            Initialize();
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

            _stack.Push<MainMenuView>().Setup(this, _orchestrator);
        }
    }
}
