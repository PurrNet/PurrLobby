using System.Collections.Generic;

namespace PurrLobby
{
    public enum LobbyVisibility
    {
        Public,
        Private
    }

    public struct LobbySettings
    {
        public string name;
        public int maxPlayers;
        public LobbyVisibility visibility;
        public Dictionary<string, string> metadata;
    }

    public struct LobbyInfo
    {
        public string id;
        public string name;
        public string code;
        public int playerCount;
        public int maxPlayers;
        public Dictionary<string, string> metadata;
    }

    public struct LobbyQuery
    {
        public int maxResults;
        public Dictionary<string, string> dataFilters;
    }
}
