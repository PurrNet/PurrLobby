using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby.GenericProviders
{
    [CreateAssetMenu(menuName = "PurrLobby/PurrNet/Matchmaker", order = -200)]
    public class LobbyMatchmaker : MatchmakingProvider
    {
        [SerializeField] private LobbyProvider _lobbyProvider;
        [SerializeField] private string _lobbyNamePrefix = "Matchmaking";

        private MatchmakingTicket? _activeTicket;
        private bool _cancelled;

        public override async Task Login(ViewStack stack)
        {
            await _lobbyProvider.Login(stack);
        }

        public override void Logout()
        {
            _lobbyProvider.Logout();
        }

        public override async void StartMatchmaking(MatchmakingRequest request, Action<MatchmakingTicketResponse> onComplete)
        {
            try
            {
                var ticket = new MatchmakingTicket
                {
                    ticketId = Guid.NewGuid().ToString()
                };

                _activeTicket = ticket;
                _cancelled = false;

                onComplete?.Invoke(MatchmakingTicketResponse.Success(ticket));
                RaiseStatusChanged(ticket, MatchmakingStatus.Searching);

                try
                {
                    var lobby = await FindOrCreateLobby(request);

                    if (_cancelled)
                        return;

                    if (lobby == null)
                    {
                        _activeTicket = null;
                        RaiseStatusChanged(ticket, MatchmakingStatus.Failed);
                        RaiseMatchmakingError(ticket, "Failed to find or create a lobby.");
                        return;
                    }

                    _activeTicket = null;
                    RaiseStatusChanged(ticket, MatchmakingStatus.Found);
                    RaiseMatchFound(ticket, new MatchResult
                    {
                        lobby = lobby
                    });
                }
                catch (Exception e)
                {
                    if (_cancelled)
                        return;

                    _activeTicket = null;
                    RaiseStatusChanged(ticket, MatchmakingStatus.Failed);
                    RaiseMatchmakingError(ticket, e.Message);
                }
            }
            catch (Exception e)
            {
                RaiseMatchmakingError(new MatchmakingTicket { ticketId = "N/A" }, $"Unexpected error starting matchmaking: {e.Message}");
                Debug.LogException(e);
            }
        }

        public override void CancelMatchmaking(MatchmakingTicket ticket, Action<APIResponse> onComplete)
        {
            if (_activeTicket == null || _activeTicket.Value.ticketId != ticket.ticketId)
            {
                onComplete?.Invoke(APIResponse.Failure("No active matchmaking with that ticket."));
                return;
            }

            _cancelled = true;
            _activeTicket = null;
            RaiseStatusChanged(ticket, MatchmakingStatus.Cancelled);
            onComplete?.Invoke(APIResponse.Success());
        }

        private async Task<ILobby> FindOrCreateLobby(MatchmakingRequest request)
        {
            var gameMode = request.gameMode ?? string.Empty;

            LobbyQuery query = null;

            if (!string.IsNullOrEmpty(gameMode))
            {
                query = new LobbyQuery()
                    .AddDataFilter("gameMode", gameMode)
                    .AddDataFilter("matchmaking", "y");
            }

            var joinResult = await _lobbyProvider.JoinRandom(query);

            if (joinResult.success)
                return joinResult.lobby;

            if (_cancelled)
                return null;

            var metadata = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(gameMode))
            {
                metadata["gameMode"] = gameMode;
                metadata["matchmaking"] = "y";
            }

            if (request.attributes != null)
            {
                foreach (var kvp in request.attributes)
                    metadata[kvp.Key] = kvp.Value;
            }

            var lobbyName = string.IsNullOrEmpty(gameMode)
                ? _lobbyNamePrefix
                : $"{_lobbyNamePrefix} - {gameMode}";

            var createResult = await _lobbyProvider.CreateLobby(new LobbySettings
            {
                name = lobbyName,
                maxPlayers = _lobbyProvider.maxPlayer,
                visibility = LobbyVisibility.Public,
                metadata = metadata
            });

            return createResult.success ? createResult.lobby : null;
        }
    }
}
