using System;

namespace PurrLobby
{
    public interface IMetadata
    {
        void SetData(string key, string value);

        string GetData(string key);

        bool TryGetData(string key, out string value);

        void RemoveData(string key);

        event Action<string, string> onDataChanged;
    }
}
