using System;
using System.Threading.Tasks;
using PurrNet.UI;
using UnityEngine;

namespace PurrLobby.PurrNet
{
    [CreateAssetMenu(menuName = "PurrLobby/PurrNet/Lobby Provider", order = -201)]
    public class PurrNetLobbyProvider : LobbyProvider
    {
        [SerializeField] private PurrNetSessionProvider _sessionProvider;
        [SerializeField] private int _maxPlayers = 4;

        public override int maxPlayer => _maxPlayers;

        public override async Task Login(ViewStack stack)
        {
            await _sessionProvider.Login(stack);
        }

        public override void Logout()
        {
            _sessionProvider.Logout();
        }

        public override Task<LobbyResponse> CreateLobby(LobbySettings settings)
        {
            throw new NotImplementedException();
        }

        public override Task<LobbyResponse> JoinLobby(string lobbyId)
        {
            throw new NotImplementedException();
        }

        public override Task<LobbyResponse> JoinLobbyByCode(string code)
        {
            throw new NotImplementedException();
        }

        public override Task<LobbyResponse> JoinRandom(LobbyQuery query = default)
        {
            throw new NotImplementedException();
        }

        public override Task<LobbyCollectionResponse> QueryLobbies(LobbyQuery query = default)
        {
            throw new NotImplementedException();
        }
    }
}
