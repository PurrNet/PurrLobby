using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PurrNet.UI;
using UnityEngine;

namespace PurrLobby
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
        public abstract Task Login(ViewStack stack);
        public abstract void Logout();
        public abstract void CreateLobby(LobbySettings settings, Action<LobbyResponse> onComplete);
        public abstract void JoinLobby(string lobbyId, Action<LobbyResponse> onComplete);
        public abstract void JoinLobbyByCode(string code, Action<LobbyResponse> onComplete);
        public abstract void JoinRandom(Action<LobbyResponse> onComplete, LobbyQuery query = default);
        public abstract void QueryLobbies(Action<LobbyCollectionResponse> onComplete, LobbyQuery query = default);
    }
}
