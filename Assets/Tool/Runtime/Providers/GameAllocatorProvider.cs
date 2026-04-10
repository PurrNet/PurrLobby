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
    }
}
