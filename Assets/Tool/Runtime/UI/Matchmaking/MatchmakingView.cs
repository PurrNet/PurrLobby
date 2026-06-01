using System;
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
                _provider.StartMatchmaking(request, OnComplete);
            }
            else
            {
                Toaster.Push($"No matchmaking provider", "This feature is not available", true);
                PopMe();
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

            StartGameFromMatch(result);
        }

        private async void StartGameFromMatch(MatchResult result)
        {
            try
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
            catch (Exception e)
            {
                Debug.LogException(e);
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
            _provider.CancelMatchmaking(_currentTicket.Value, OnCanceled);
        }

        private void OnCanceled(APIResponse resp)
        {
            _cancelling = false;
            if (!resp.success)
                 Toaster.Push($"Failed to cancel", resp.error, true);
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

        private void OnComplete(MatchmakingTicketResponse response)
        {
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
    }
}
