using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class MatchmakingView : MonoView
    {
        [SerializeField] private TMPro.TMP_Text _message;

        private MenuOrchestrator _orchestrator;
        private MatchmakingProvider _provider;
        private bool _cancelling;

        public void Setup(MenuOrchestrator orchestrator, MatchmakingRequest request)
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
            if (_orchestrator.lobbyProvider && result.lobby != null)
                parentStack.Replace<LobbyView>().Setup(result.lobby);
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
