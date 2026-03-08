using System;
using System.Threading.Tasks;
using PurrNet.UI;
using UnityEngine;

namespace PurrLobby
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
    }

    public abstract class GameStarterProvider : ScriptableObject
    {
        public abstract Task Login(ViewStack stack);

        public abstract void Logout();

        public abstract void StartGame(ILobby lobby, Action<GameStartResponse> onComplete);
    }
}
