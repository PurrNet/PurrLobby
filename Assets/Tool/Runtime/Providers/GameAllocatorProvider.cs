using System;
using System.Threading.Tasks;
using PurrNet.Logging;
using PurrNet.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        /// <summary>
        /// Loads the game scene and captures the menu scene to return to. Concrete allocators call this
        /// from their <see cref="LoadGame(ILobby)"/> instead of loading the scene themselves.
        /// </summary>
        protected Task LoadGameScene(string gameScene)
        {
            if (string.IsNullOrEmpty(gameScene))
                throw new Exception($"Game scene is not set. Please set the game scene in the inspector of `{name}`.");

            var orch = GameOrchestrator.active;
            if (orch && string.IsNullOrEmpty(orch.menuScene))
                orch.menuScene = SceneManager.GetActiveScene().name;

            var asyncOp = SceneManager.LoadSceneAsync(gameScene);
            if (asyncOp == null)
            {
                PurrLogger.LogError($"Loading scene `{gameScene}` failed.");
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>();
            asyncOp.completed += _ =>
            {
                GameSession.EnsureInScene(SceneManager.GetActiveScene());
                tcs.SetResult(true);
            };
            return tcs.Task;
        }
    }
}
