using System;
using System.Collections.Generic;

namespace PurrLobby
{
    public interface ILobby
    {
        string id { get; }

        IPlayer localPlayer { get; }

        IPlayer host { get; }

        int maxPlayers { get; }

        IReadOnlyList<IPlayer> players { get; }

        IMetadata lobbyData { get; }

        ILobbyChat chat { get; }

        void KickPlayer(IPlayer player);

        void LeaveLobby();

        event Action<IPlayer> onPlayerJoined;

        event Action<IPlayer> onPlayerLeft;

        event Action<IPlayer> onPlayerUpdated;

        event Action<IPlayer> onHostChanged;

        event Action onLobbyDestroyed;
    }
}
