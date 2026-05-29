using PurrNet.Logging;
using UnityEngine;

namespace PurrNet.Lobby
{
    /// <summary>
    /// A scene network object in the game scene. Lets the server end the game
    /// for every player via <see cref="EndGame"/>.
    /// </summary>
    public class GameOverBroadcaster : NetworkIdentity
    {
        [Tooltip("GameSession that performs the return to menu. Optional - falls back to GameSession.Instance.")]
        [SerializeField] private GameSession _session;

        /// <summary>
        /// Server-only. Ends the game and returns every player to the menu.
        /// </summary>
        public void EndGame()
        {
            if (!isSpawned)
            {
                PurrLogger.LogError("`GameOverBroadcaster.EndGame` was called before the object spawned.", this);
                return;
            }

            if (!isServer)
            {
                PurrLogger.LogError("`GameOverBroadcaster.EndGame` must be called on the server.", this);
                return;
            }

            EndGameRpc();
        }

        [ObserversRpc(runLocally: true)]
        private void EndGameRpc()
        {
            var session = _session ? _session : GameSession.Instance;

            if (session)
                session.GameEnded();
            else
                PurrLogger.LogError("`GameOverBroadcaster` could not find a `GameSession` to end the game.", this);
        }
    }
}
