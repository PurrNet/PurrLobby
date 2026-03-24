using System;
using System.Collections.Generic;

namespace PurrNet.Lobby
{
    public interface ILobby
    {
        string id { get; }

        string joinCode { get; }

        IPlayer localPlayer { get; }

        IPlayer host { get; }

        int maxPlayers { get; }

        IReadOnlyList<IPlayer> players { get; }

        IMetadata lobbyData { get; }

        bool isLobbyJoinable { get; }

        ILobbyChat chat { get; }

        bool isHost { get; }

        void KickPlayer(IPlayer player);

        void SetIsLobbyJoinable(bool isJoinable);

        void LeaveLobby();

        event Action<IPlayer> onPlayerJoined;

        event Action<IPlayer> onPlayerLeft;

        event Action<IPlayer> onPlayerUpdated;

        event Action<IPlayer> onHostChanged;

        event Action onLobbyDestroyed;
    }
}
