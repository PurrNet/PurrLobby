using System;
using System.Threading.Tasks;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class MatchmakingView : MonoView
    {
        [SerializeField] private TMPro.TMP_Text _message;

        private GameOrchestrator _orchestrator;
        private MatchmakingProvider _provider;
        private bool _cancelling;
        private bool _failed;

        public void Setup(GameOrchestrator orchestrator, MatchmakingRequest request)
        {
            _message.text = $"Matchmaking for {request.gameMode}: Queued";
            _orchestrator = orchestrator;
            _provider = orchestrator.matchmakingProvider;

            if (_provider)
            {
                _provider.onStatusChanged += OnStatusChanged;
                _provider.onMatchFound += OnMatchFound;
                _provider.onMatchmakingError += OnMatchmakingError;
                StartMatchmakingAsync(request).Forget("[MatchmakingView] StartMatchmaking failed");
            }
            else
            {
                Toaster.Push($"No matchmaking provider", "This feature is not available", true);
                PopMe();
            }
        }

        private async Task StartMatchmakingAsync(MatchmakingRequest request)
        {
            var response = await _provider.StartMatchmaking(request);

            if (!this)
                return;

            if (!response.success)
            {
                Toaster.Push($"Matchmaking failed", response.error, true);
                PopMe();
            }
            else
            {
                _currentTicket = response.ticket;
            }
        }

        public override void OnPopped()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private bool _unsubscribed;

        private void Unsubscribe()
        {
            if (_unsubscribed || !_provider)
                return;

            _unsubscribed = true;
            _provider.onStatusChanged -= OnStatusChanged;
            _provider.onMatchFound -= OnMatchFound;
            _provider.onMatchmakingError -= OnMatchmakingError;
        }

        private void OnMatchFound(MatchmakingTicket ticket, MatchResult result)
        {
            if (result.lobby != null)
            {
                parentStack.ReplaceOrPush<LobbyView>(this).Setup(result.lobby, _orchestrator);
                return;
            }

            StartGameFromMatch(result).Forget("[MatchmakingView] StartGameFromMatch failed");
        }

        private async Task StartGameFromMatch(MatchResult result)
        {
            _orchestrator.activeLobby = null;
            var loadingView = parentStack.ReplaceOrPush<LoadingView>(this);

            try
            {
                loadingView.Setup("Allocating game...");

                var response = await _orchestrator.gameAllocator.AllocateGame(result);

                if (!response.success)
                    throw new Exception(response.error);

                loadingView.Setup("Loading game...");
                await _orchestrator.gameAllocator.LoadGame(result);

                _orchestrator.gameAllocator.Connect(response.connection, result.isHost);
            }
            catch (Exception e)
            {
                Toaster.PushError("Failed to start game", e);
                if (_orchestrator.gameAllocator)
                    Debug.LogException(e, _orchestrator.gameAllocator);
            }
            finally
            {
                if (loadingView)
                    loadingView.PopMe();
            }
        }

        public void Cancel()
        {
            if (_cancelling)
                return;

            if (!_currentTicket.HasValue)
            {
                Toaster.Push($"Failed to cancel", "Waiting for ticket", true);
                return;
            }

            _cancelling = true;
            CancelAsync(_currentTicket.Value).Forget("[MatchmakingView] CancelMatchmaking failed");
        }

        private async Task CancelAsync(MatchmakingTicket ticket)
        {
            var response = await _provider.CancelMatchmaking(ticket);

            if (!this)
                return;

            _cancelling = false;
            if (!response.success)
                 Toaster.Push($"Failed to cancel", response.error, true);
            else PopMe();
        }

        private void OnStatusChanged(MatchmakingTicket ticket, MatchmakingStatus status)
        {
            _message.text = $"Matchmaking: {status}";

            if (status is MatchmakingStatus.Failed && !_failed)
            {
                _failed = true;
                Toaster.PushError("Matchmaking failed", "The matchmaker reported a failure.");
                PopMe();
            }
        }

        private void OnMatchmakingError(MatchmakingTicket ticket, string error)
        {
            if (_failed)
                return;

            _failed = true;
            Toaster.Push("Matchmaking failed", string.IsNullOrEmpty(error) ? "Unknown error." : error, true);
            PopMe();
        }

        private MatchmakingTicket? _currentTicket;
    }
}
