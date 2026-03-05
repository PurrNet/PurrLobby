using System;
using UnityEngine;

namespace PurrLobby
{
    public struct MatchmakingTicketResponse
    {
        public bool success;
        public string error;
        public MatchmakingTicket ticket;

        public static MatchmakingTicketResponse Success(MatchmakingTicket ticket) =>
            new MatchmakingTicketResponse { success = true, ticket = ticket };
        public static MatchmakingTicketResponse Failure(string error) =>
            new MatchmakingTicketResponse { success = false, error = error };
    }

    public abstract class MatchmakingProvider : ScriptableObject
    {
        public abstract void Initialize(MenuOrchestrator menuOrchestrator);

        public abstract void StartMatchmaking(MatchmakingRequest request, Action<MatchmakingTicketResponse> onComplete);
        public abstract void CancelMatchmaking(MatchmakingTicket ticket, Action<APIResponse> onComplete);

        public event Action<MatchmakingTicket, MatchResult> onMatchFound;
        public event Action<MatchmakingTicket, MatchmakingStatus> onStatusChanged;
        public event Action<MatchmakingTicket, string> onMatchmakingError;

        protected void RaiseMatchFound(MatchmakingTicket ticket, MatchResult result) =>
            onMatchFound?.Invoke(ticket, result);
        protected void RaiseStatusChanged(MatchmakingTicket ticket, MatchmakingStatus status) =>
            onStatusChanged?.Invoke(ticket, status);
        protected void RaiseMatchmakingError(MatchmakingTicket ticket, string error) =>
            onMatchmakingError?.Invoke(ticket, error);
    }
}
