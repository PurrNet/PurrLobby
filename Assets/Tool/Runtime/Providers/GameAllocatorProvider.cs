using System.Threading.Tasks;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public struct GameStartResponse
    {
        public bool success;
        public string error;
        public ConnectionInfo connection;

        public static GameStartResponse Success(ConnectionInfo connection) =>
            new GameStartResponse { success = true, connection = connection };
        public static GameStartResponse Failure(string error) =>
            new GameStartResponse { success = false, error = error };

        public override string ToString()
        {
            return $"success: {success}, error: {error}, connection: {{ {connection} }}";
        }
    }

    public abstract class GameAllocatorProvider : ScriptableObject
    {
        public abstract Task Login(ViewStack stack);

        public abstract void Logout();

        public abstract Task<GameStartResponse> AllocateGame(ILobby lobby);

        public abstract Task LoadGame(ILobby lobby);

        public abstract void Connect(ConnectionInfo connection, bool shouldBeHost);

        /// <summary>Allocates a game from a matchmaking result. If the result has a lobby, delegates to the lobby overload; otherwise returns the pre-populated connection info.</summary>
        public virtual Task<GameStartResponse> AllocateGame(MatchResult matchResult)
        {
            if (matchResult.lobby != null)
                return AllocateGame(matchResult.lobby);
            return Task.FromResult(GameStartResponse.Success(matchResult.connection));
        }

        /// <summary>Loads the game scene from a matchmaking result. Delegates to the lobby overload by default.</summary>
        public virtual Task LoadGame(MatchResult matchResult)
        {
            return LoadGame(matchResult.lobby);
        }
    }
}
