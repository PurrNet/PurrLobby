using System;
using System.Collections;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class LobbyBrowserView : MonoView
    {
        [SerializeField] private RectTransform _window;
        [SerializeField] private LobbyInfoEntry _lobbyInfoEntry;
        [SerializeField] private RectTransform _lobbyContent;
        [Space]
        [SerializeField] private LoadingOverlay _loadingOverlay;

        private LobbyProvider _lobbyProvider;
        private UIPool<LobbyInfoEntry> _lobbyEntries;
        private bool _successfulExit;

        private readonly LobbyQuery _query = new LobbyQuery()
            .AddStringFilter("matchmaking", FilterComparison.NotEqual, "y");

        public void Setup(LobbyProvider lobbyProvider)
        {
            _lobbyProvider = lobbyProvider;
            _lobbyEntries ??= new UIPool<LobbyInfoEntry>(_lobbyInfoEntry, _lobbyContent);
            Refresh();
        }

        public void TriggerRefresh() => Refresh();

        private async void Refresh()
        {
            try
            {
                _loadingOverlay.Toggle(true);
                var res = await _lobbyProvider.QueryLobbies(_query);
                if (!this)
                    return;
                SetupView(res);
            }
            catch (Exception e)
            {
                Toaster.PushError("Failed to fetch lobbies", e);
                Debug.LogException(e);
            }
            finally
            {
                _loadingOverlay.Toggle(false);
            }
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

        private async void OnClickedLobby(LobbyInfo info)
        {
            try
            {
                _loadingOverlay.Toggle(true);
                var res = await _lobbyProvider.JoinLobby(info.id);
                if (!res.success)
                {
                    Toaster.PushError("Failed to join lobby", res.error);
                    return;
                }

                _successfulExit = true;
                parentStack.ReplaceOrPush<LobbyView>(this).Setup(res.lobby, _lobbyProvider);
            }
            catch (Exception e)
            {
                Toaster.PushError("Failed to join lobby", e);
                Debug.LogException(e);
            }
            finally
            {
                _loadingOverlay.Toggle(false);
            }
        }

        protected override IEnumerator OnEnterTransition()
        {
            var fade = ViewTransitions.FadeIn(this);
            var slide = ViewTransitions.SlideFromRight(_window);
            return ViewTransitions.Parallel(fade, slide);
        }

        protected override IEnumerator OnExitTransition()
        {
            if (_successfulExit)
            {
                var fade = ViewTransitions.FadeOut(this);
                var slide = ViewTransitions.SlideToLeft(_window);
                return ViewTransitions.Parallel(fade, slide);
            }
            else
            {
                var fade = ViewTransitions.FadeOut(this);
                var slide = ViewTransitions.SlideToRight(_window);
                return ViewTransitions.Parallel(fade, slide);
            }
        }
    }
}
