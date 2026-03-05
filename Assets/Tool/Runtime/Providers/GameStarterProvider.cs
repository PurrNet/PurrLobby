using System;
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
        public abstract void Initialize(MenuOrchestrator menuOrchestrator);

        public abstract void StartGame(ILobby lobby, Action<GameStartResponse> onComplete);
    }
}
