using System.Collections.Generic;
using System.Threading.Tasks;

namespace PurrLobby
{
    public interface ILobbyProvider
    {
        Task<ILobby> CreateLobby(LobbySettings settings);

        Task<ILobby> JoinLobby(string lobbyId);

        Task<ILobby> JoinLobbyByCode(string code);

        Task<ILobby> JoinRandom(LobbyQuery query = default);

        Task<IReadOnlyList<LobbyInfo>> QueryLobbies(LobbyQuery query = default);
    }
}
