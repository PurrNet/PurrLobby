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

        public override async Task Login(ViewStack stack)
        {
            await _sessionProvider.Login(stack);
        }

        public override void Logout()
        {
            _sessionProvider.Logout();
        }

        public override void CreateLobby(LobbySettings settings, Action<LobbyResponse> onComplete)
        {
            throw new NotImplementedException();
        }

        public override void JoinLobby(string lobbyId, Action<LobbyResponse> onComplete)
        {
            throw new NotImplementedException();
        }

        public override void JoinLobbyByCode(string code, Action<LobbyResponse> onComplete)
        {
            throw new NotImplementedException();
        }

        public override void JoinRandom(Action<LobbyResponse> onComplete, LobbyQuery query = default)
        {
            throw new NotImplementedException();
        }

        public override void QueryLobbies(Action<LobbyCollectionResponse> onComplete, LobbyQuery query = default)
        {
            throw new NotImplementedException();
        }
    }
}
