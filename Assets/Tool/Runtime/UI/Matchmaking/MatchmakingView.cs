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

        public void Setup(GameOrchestrator orchestrator, MatchmakingRequest request)
        {
            _message.text = $"Matchmaking for {request.gameMode}: Queued";
            _orchestrator = orchestrator;
            _provider = orchestrator.matchmakingProvider;

            if (_provider)
            {
                _provider.StartMatchmaking(request, OnComplete);
                _provider.onStatusChanged += OnStatusChanged;
                _provider.onMatchFound += OnMatchFound;
            }
            else
            {
                Toaster.Push($"No matchmaking provider", "This feature is not available", true);
                PopMe();
            }
        }

        public override void OnPopped()
        {
            if (_provider)
            {
                _provider.onStatusChanged -= OnStatusChanged;
                _provider.onMatchFound -= OnMatchFound;
            }
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
            LoadingView loadingView = null;

            try
            {
                loadingView = parentStack.Push<LoadingView>();
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
                PopMe();
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
            _message.text = $"Matchmaking for {ticket.ticketId}: {status}";
            if (status is MatchmakingStatus.Failed)
            {
                Toaster.PushError("Failed to matchmake", $"{ticket.ticketId}");
                PopMe();
            }
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
