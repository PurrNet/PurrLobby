using System.Threading;
using System.Threading.Tasks;

namespace PurrLobby
{
    public interface IGameStarter
    {
        Task<ConnectionInfo> StartGame(GameStartRequest request, CancellationToken cancellation = default);
    }
}
