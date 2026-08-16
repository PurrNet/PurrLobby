using System;

namespace PurrNet.Lobby
{
    /// <summary>
    /// Optional features a <see cref="LobbyProvider"/> may support.
    /// The UI uses these flags to hide buttons for unsupported actions.
    /// Defaults to <see cref="All"/> so providers only need to override when restricting capabilities.
    /// </summary>
    [Flags]
    public enum LobbyCapabilities
    {
        None = 0,

        /// <summary>Provider can host a new lobby via <see cref="LobbyProvider.CreateLobby"/>.</summary>
        CreateLobby = 1 << 0,

        /// <summary>Provider can join a known lobby id via <see cref="LobbyProvider.JoinLobby"/>.</summary>
        JoinLobbyById = 1 << 1,

        /// <summary>Provider can resolve a short share code via <see cref="LobbyProvider.JoinLobbyByCode"/>.</summary>
        JoinLobbyByCode = 1 << 2,

        /// <summary>Provider can quick-join a random matching lobby via <see cref="LobbyProvider.JoinRandom"/>.</summary>
        JoinRandom = 1 << 3,

        /// <summary>Provider can list lobbies via <see cref="LobbyProvider.QueryLobbies"/>.</summary>
        QueryLobbies = 1 << 4,

        /// <summary>Provider honors <see cref="LobbySettings.visibility"/> when creating lobbies.</summary>
        PrivateLobbies = 1 << 5,

        All = CreateLobby | JoinLobbyById | JoinLobbyByCode | JoinRandom | QueryLobbies | PrivateLobbies,
    }

    public static class LobbyCapabilitiesExtensions
    {
        public static bool Has(this LobbyCapabilities flags, LobbyCapabilities flag) => (flags & flag) == flag;
    }
}
