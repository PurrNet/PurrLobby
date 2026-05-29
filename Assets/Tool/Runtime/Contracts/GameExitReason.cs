namespace PurrNet.Lobby
{
    /// <summary>
    /// Why a player left the game scene. Stored on <see cref="GameOrchestrator"/>
    /// so the menu can react after the return.
    /// </summary>
    public enum GameExitReason
    {
        /// <summary>Normal launch - nothing to report.</summary>
        None,

        /// <summary>The player chose to leave the game.</summary>
        LeftByChoice,

        /// <summary>The server ended the game for everyone.</summary>
        GameOver,

        /// <summary>Disconnected, and reconnect attempts were exhausted.</summary>
        ConnectionLost
    }
}
