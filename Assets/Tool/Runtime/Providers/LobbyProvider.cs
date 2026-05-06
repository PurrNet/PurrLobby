using System.Collections.Generic;
using System.Threading.Tasks;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public struct LobbyResponse
    {
        public bool success;
        public string error;
        public ILobby lobby;

        public static LobbyResponse Success(ILobby lobby) => new LobbyResponse { success = true, lobby = lobby };

        public static LobbyResponse Failure(string error) => new LobbyResponse { success = false, error = error };
    }

    public struct LobbyCollectionResponse
    {
        public bool success;
        public string error;
        public IReadOnlyList<LobbyInfo> lobbies;

        public static LobbyCollectionResponse Success(IReadOnlyList<LobbyInfo> lobbies) =>
            new LobbyCollectionResponse { success = true, lobbies = lobbies };

        public static LobbyCollectionResponse Failure(string error) =>
            new LobbyCollectionResponse { success = false, error = error };
    }

    public abstract class LobbyProvider : ScriptableObject
    {
        public abstract int maxPlayer { get; }

        /// <summary>
        /// Which optional features this provider supports. The UI hides actions for missing capabilities.
        /// </summary>
        public virtual LobbyCapabilities capabilities => LobbyCapabilities.All;

        public abstract Task Login(ViewStack stack);

        public abstract void Logout();

        public abstract Task<LobbyResponse> CreateLobby(LobbySettings settings);

        public abstract Task<LobbyResponse> JoinLobby(string lobbyId);

        public abstract Task<LobbyResponse> JoinLobbyByCode(string code);

        public abstract Task<LobbyResponse> JoinRandom(LobbyQuery query = null);

        public abstract Task<LobbyCollectionResponse> QueryLobbies(LobbyQuery query = null);
    }
}
