using System.Collections.Generic;

namespace PurrNet.Lobby
{
    public enum LobbyVisibility
    {
        Public,
        Private
    }

    public enum FilterComparison
    {
        Equal,
        NotEqual,
        LessThan,
        GreaterThan,
        LessThanOrEqual,
        GreaterThanOrEqual
    }

    public struct StringFilter
    {
        public string key;
        public FilterComparison op;
        public string value;
    }

    public struct NumericalFilter
    {
        public string key;
        public FilterComparison op;
        public int value;
    }

    public struct NearValueSort
    {
        public string key;
        public int target;
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
        public bool joinable;
        public int playerCount;
        public int maxPlayers;
        public Dictionary<string, string> metadata;
    }

    public class LobbyQuery
    {
        public int slotsAvailable;
        public List<StringFilter> stringFilters;
        public List<NumericalFilter> numericalFilters;
        public List<NearValueSort> nearValueSorts;

        public LobbyQuery AddStringFilter(string key, FilterComparison op, string value)
        {
            stringFilters ??= new List<StringFilter>();
            stringFilters.Add(new StringFilter { key = key, op = op, value = value });
            return this;
        }

        public LobbyQuery AddNumericalFilter(string key, FilterComparison op, int value)
        {
            numericalFilters ??= new List<NumericalFilter>();
            numericalFilters.Add(new NumericalFilter { key = key, op = op, value = value });
            return this;
        }

        public LobbyQuery AddNearValueSort(string key, int target)
        {
            nearValueSorts ??= new List<NearValueSort>();
            nearValueSorts.Add(new NearValueSort { key = key, target = target });
            return this;
        }

        public LobbyQuery SetSlotsAvailable(int slots)
        {
            slotsAvailable = slots;
            return this;
        }

        /// <summary>Adds an exact-match string filter.</summary>
        public LobbyQuery AddDataFilter(string key, string value)
        {
            return AddStringFilter(key, FilterComparison.Equal, value);
        }
    }
}
