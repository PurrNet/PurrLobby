using System;
using UnityEngine;

namespace PurrLobby
{
    [CreateAssetMenu(menuName = "PurrNet/Lobby/Providers/PurrNet/Matchmaking Provider")]
    public sealed class PurrMatchmaker : MatchmakingProvider
    {
        public override void Initialize(MenuOrchestrator menuOrchestrator) { }

        public override void StartMatchmaking(MatchmakingRequest request, Action<MatchmakingTicketResponse> onComplete)
        {
            throw new NotImplementedException();
        }

        public override void CancelMatchmaking(MatchmakingTicket ticket, Action<APIResponse> onComplete)
        {
            throw new NotImplementedException();
        }
    }
}
