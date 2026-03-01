using System;
using UnityEngine;

namespace PurrLobby
{
    [CreateAssetMenu(menuName = "PurrNet/Lobby/Providers/PurrNet/Lobby Provider")]
    public sealed class PurrLobbyProvider : LobbyProvider
    {
        [SerializeField] PurrSession _session;

        PurrLobbyClient _client;

        PurrLobbyClient GetClient()
        {
            return _client ??= new PurrLobbyClient(_session);
        }

        public override async void CreateLobby(LobbySettings settings, Action<LobbyResponse> onComplete)
        {
            try
            {
                var lobby = await GetClient().CreateLobbyAsync(settings);
                onComplete?.Invoke(LobbyResponse.Success(lobby));
            }
            catch (Exception ex)
            {
                onComplete?.Invoke(LobbyResponse.Failure(ex.Message));
            }
        }

        public override async void JoinLobby(string lobbyId, Action<LobbyResponse> onComplete)
        {
            try
            {
                var lobby = await GetClient().JoinLobbyAsync(lobbyId);
                onComplete?.Invoke(LobbyResponse.Success(lobby));
            }
            catch (Exception ex)
            {
                onComplete?.Invoke(LobbyResponse.Failure(ex.Message));
            }
        }

        public override async void JoinLobbyByCode(string code, Action<LobbyResponse> onComplete)
        {
            try
            {
                var lobby = await GetClient().JoinLobbyByCodeAsync(code);
                onComplete?.Invoke(LobbyResponse.Success(lobby));
            }
            catch (Exception ex)
            {
                onComplete?.Invoke(LobbyResponse.Failure(ex.Message));
            }
        }

        public override async void JoinRandom(Action<LobbyResponse> onComplete, LobbyQuery query = default)
        {
            try
            {
                var lobby = await GetClient().JoinRandomAsync(query);
                onComplete?.Invoke(LobbyResponse.Success(lobby));
            }
            catch (Exception ex)
            {
                onComplete?.Invoke(LobbyResponse.Failure(ex.Message));
            }
        }

        public override async void QueryLobbies(Action<LobbyCollectionResponse> onComplete, LobbyQuery query = default)
        {
            try
            {
                var lobbies = await GetClient().QueryLobbiesAsync(query);
                onComplete?.Invoke(LobbyCollectionResponse.Success(lobbies));
            }
            catch (Exception ex)
            {
                onComplete?.Invoke(LobbyCollectionResponse.Failure(ex.Message));
            }
        }
    }
}
