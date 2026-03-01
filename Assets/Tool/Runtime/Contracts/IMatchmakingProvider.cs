using System;
using System.Threading.Tasks;

namespace PurrLobby
{
    public interface IMatchmakingProvider
    {
        Task<MatchmakingTicket> StartMatchmaking(MatchmakingRequest request);

        Task CancelMatchmaking(MatchmakingTicket ticket);

        event Action<MatchmakingTicket, MatchResult> onMatchFound;

        event Action<MatchmakingTicket, MatchmakingStatus> onStatusChanged;

        event Action<MatchmakingTicket, string> onMatchmakingError;
    }
}
