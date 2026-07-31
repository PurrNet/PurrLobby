using System;

namespace PurrNet.Lobby
{
    /// <summary>
    /// One entry in a player's context menu: what the option looks like and what
    /// it does. Games append their own (mute, view profile, ...) via
    /// <see cref="PlayerEntry.onBuildContextActions"/>.
    /// </summary>
    public struct PlayerContextAction
    {
        public ContextOption option;
        public Action<ILobby, IPlayer> action;
    }
}
