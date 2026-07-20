using System.Threading.Tasks;
using PurrNet.UI;
using PurrNet.UI.HeroUI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class LobbyBrowserView : SlidePageView
    {
        [SerializeField] private LobbyInfoEntry _lobbyInfoEntry;
        [SerializeField] private RectTransform _lobbyContent;
        [Space]
        [SerializeField] private LoadingOverlay _loadingOverlay;

        private GameOrchestrator _orchestrator;
        private UIPool<LobbyInfoEntry> _lobbyEntries;

        private readonly LobbyQuery _query = new LobbyQuery()
            .AddStringFilter("matchmaking", FilterComparison.NotEqual, "y");

        public void Setup(GameOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
            _lobbyEntries ??= new UIPool<LobbyInfoEntry>(_lobbyInfoEntry, _lobbyContent);
            TriggerRefresh();
        }

        public void TriggerRefresh()
        {
            UiTask.Run(RefreshAsync, "Failed to fetch lobbies", _loadingOverlay);
        }

        private async Task RefreshAsync()
        {
            var res = await _orchestrator.lobbyProvider.QueryLobbies(_query);

            if (!this)
                return;

            SetupView(res);
        }

        private void SetupView(LobbyCollectionResponse query)
        {
            if (!query.success)
                return;

            _lobbyEntries.ResetCounter();

            for (var i = 0; i < query.lobbies.Count; i++)
            {
                var info = query.lobbies[i];
                _lobbyEntries.GetInstance().Setup(info, OnClickedLobby);
            }

            _lobbyEntries.DiscardRest();
        }

        private void OnClickedLobby(LobbyInfo info)
        {
            UiTask.Run(() => JoinAsync(info), "Failed to join lobby", _loadingOverlay);
        }

        private async Task JoinAsync(LobbyInfo info)
        {
            var res = await _orchestrator.lobbyProvider.JoinLobby(info.id);

            if (!this)
                return;

            if (!res.success)
            {
                Toaster.PushError("Failed to join lobby", res.error);
                return;
            }

            successfulExit = true;
            parentStack.ReplaceOrPush<LobbyView>(this).Setup(res.lobby, _orchestrator);
        }
    }
}
