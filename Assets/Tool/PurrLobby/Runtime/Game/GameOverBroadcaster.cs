using PurrNet.Logging;

namespace PurrNet.Lobby
{
    /// <summary>
    /// A scene network object in the game scene. Lets the server end the game
    /// for every player via <see cref="EndGame"/>.
    /// </summary>
    public class GameOverBroadcaster : NetworkIdentity
    {
        private GameSession _resolved;

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
            var session = ResolveSession();

            if (session)
                session.GameEnded();
            else
                PurrLogger.LogError("`GameOverBroadcaster` could not find a `GameSession` to end the game.", this);
        }

        /// <summary>Resolves the GameSession in this broadcaster's own scene, so a server hosting several scenes ends the right one.</summary>
        private GameSession ResolveSession()
        {
            if (_resolved || GameSession.TryGet(gameObject.scene, out _resolved))
                return _resolved;

            return GameSession.instance;
        }
    }
}
