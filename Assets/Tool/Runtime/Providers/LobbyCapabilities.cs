using System;

namespace PurrNet.Lobby
{
    /// <summary>
    /// Optional features a <see cref="LobbyProvider"/> may support. Backends differ in what they can
    /// offer — Nakama for example has no built-in lobby directory, so it cannot list lobbies or join
    /// by code. The UI uses these flags to hide buttons/views that would otherwise call into an
    /// unsupported method.
    ///
    /// Providers should set <see cref="LobbyProvider.capabilities"/> to the union of features they
    /// implement. The default is <see cref="All"/> so existing providers don't need updates.
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

        All = CreateLobby | JoinLobbyById | JoinLobbyByCode | JoinRandom | QueryLobbies,
    }

    public static class LobbyCapabilitiesExtensions
    {
        public static bool Has(this LobbyCapabilities flags, LobbyCapabilities flag) => (flags & flag) == flag;
    }
}
