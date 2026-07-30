using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace PurrNet.Lobby.GenericProviders
{
    [CreateAssetMenu(menuName = "PurrLobby/PurrNet/Matchmaker", order = -200)]
    public class LobbyMatchmaker : MatchmakingProvider
    {
        [SerializeField] private LobbyProvider _lobbyProvider;
        [SerializeField] private string _lobbyNamePrefix = "Matchmaking";

        private bool _cancelled;

        public override Task Logout()
        {
            return _lobbyProvider ? _lobbyProvider.Logout() : Task.CompletedTask;
        }

        public override Task<MatchmakingTicketResponse> StartMatchmaking(MatchmakingRequest request)
        {
            _cancelled = false;
            var ticket = BeginTicket();

            MatchIntoLobbyAsync(ticket, request).Forget($"[{name}] Matchmaking failed");

            return Task.FromResult(MatchmakingTicketResponse.Success(ticket));
        }

        public override Task<APIResponse> CancelMatchmaking(MatchmakingTicket ticket)
        {
            if (!TryConsumeActiveTicket(ticket))
                return Task.FromResult(APIResponse.Failure("No active matchmaking with that ticket."));

            _cancelled = true;
            CancelLocally(ticket);
            return Task.FromResult(APIResponse.Success());
        }

        private async Task MatchIntoLobbyAsync(MatchmakingTicket ticket, MatchmakingRequest request)
        {
            try
            {
                var lobby = await FindOrCreateLobby(request);

                if (_cancelled || IsStale(ticket))
                {
                    // The cancel (or a restart) landed while we were joining; don't squat in the lobby.
                    lobby?.LeaveLobby();
                    return;
                }

                if (lobby == null)
                {
                    FailMatch(ticket, "Failed to find or create a lobby.");
                    return;
                }

                CompleteMatch(ticket, new MatchResult
                {
                    lobby = lobby
                });
            }
            catch (Exception e)
            {
                if (_cancelled || IsStale(ticket))
                    return;

                FailMatch(ticket, e.Message);
            }
        }

        private async Task<ILobby> FindOrCreateLobby(MatchmakingRequest request)
        {
            var gameMode = request.gameMode ?? string.Empty;
            var caps = _lobbyProvider.capabilities;

            if (caps.Has(LobbyCapabilities.JoinRandom))
            {
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
            }

            if (_cancelled)
                return null;

            if (!caps.Has(LobbyCapabilities.CreateLobby))
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
