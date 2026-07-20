using System;
using System.Threading.Tasks;
using UnityEngine;

namespace PurrNet.Lobby
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
        /// <summary>Called once after the session provider has logged in.</summary>
        public virtual Task Initialize() => Task.CompletedTask;

        /// <summary>Provider-local cleanup (cancels any active ticket).</summary>
        public virtual Task Logout() => Task.CompletedTask;

        /// <summary>
        /// Starts matchmaking. The returned response only acknowledges the ticket;
        /// results arrive later via <see cref="onMatchFound"/> / <see cref="onMatchmakingError"/>.
        /// </summary>
        public abstract Task<MatchmakingTicketResponse> StartMatchmaking(MatchmakingRequest request);

        public abstract Task<APIResponse> CancelMatchmaking(MatchmakingTicket ticket);

        public event Action<MatchmakingTicket, MatchResult> onMatchFound;
        public event Action<MatchmakingTicket, MatchmakingStatus> onStatusChanged;
        public event Action<MatchmakingTicket, string> onMatchmakingError;

        /// <summary>The ticket currently being matched, if any.</summary>
        protected MatchmakingTicket? activeTicket { get; private set; }

        /// <summary>Creates and stores a new active ticket and raises Searching.</summary>
        protected MatchmakingTicket BeginTicket(string externalId = null)
        {
            var ticket = new MatchmakingTicket { ticketId = externalId ?? Guid.NewGuid().ToString("N") };
            activeTicket = ticket;
            RaiseStatusChanged(ticket, MatchmakingStatus.Searching);
            return ticket;
        }

        /// <summary>True if this ticket is no longer the active one (cancelled or replaced).</summary>
        protected bool IsStale(in MatchmakingTicket ticket)
        {
            return activeTicket is not { } active || active.ticketId != ticket.ticketId;
        }

        /// <summary>Clears the active ticket iff it matches. Used by Cancel implementations.</summary>
        protected bool TryConsumeActiveTicket(in MatchmakingTicket ticket)
        {
            if (IsStale(ticket))
                return false;

            activeTicket = null;
            return true;
        }

        /// <summary>Clears the active ticket, raises Found, then onMatchFound.</summary>
        protected void CompleteMatch(MatchmakingTicket ticket, MatchResult result)
        {
            activeTicket = null;
            RaiseStatusChanged(ticket, MatchmakingStatus.Found);
            onMatchFound?.Invoke(ticket, result);
        }

        /// <summary>Clears the active ticket, raises onMatchmakingError, then Failed.</summary>
        protected void FailMatch(MatchmakingTicket ticket, string error)
        {
            activeTicket = null;
            onMatchmakingError?.Invoke(ticket, error);
            RaiseStatusChanged(ticket, MatchmakingStatus.Failed);
        }

        /// <summary>Clears the active ticket and raises Cancelled.</summary>
        protected void CancelLocally(MatchmakingTicket ticket)
        {
            activeTicket = null;
            RaiseStatusChanged(ticket, MatchmakingStatus.Cancelled);
        }

        protected void RaiseStatusChanged(MatchmakingTicket ticket, MatchmakingStatus status) =>
            onStatusChanged?.Invoke(ticket, status);
    }
}
