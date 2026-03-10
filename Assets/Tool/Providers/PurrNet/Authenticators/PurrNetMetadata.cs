using System;
using System.Collections.Generic;
using PurrNet.Services;

namespace PurrLobby.PurrNet
{
    public class PurrNetMetadata : IMetadata
    {
        private readonly Dictionary<string, string> _data = new ();

        private readonly LobbyService _service;
        private readonly string _lobbyId;
        private readonly bool _isPlayerMetadata;

        public PurrNetMetadata(LobbyService service, string lobbyId, bool isPlayerMetadata)
        {
            _service = service;
            _lobbyId = lobbyId;
            _isPlayerMetadata = isPlayerMetadata;
        }

        public void SetData(string key, string value)
        {
            _data[key] = value;
            _ = _isPlayerMetadata ?
                _service.SetPlayerMetadataAsync(_lobbyId, _data) :
                _service.SetMetadataAsync(_lobbyId, _data);
        }

        public string GetData(string key)
        {
            return _data[key];
        }

        public bool TryGetData(string key, out string value)
        {
            return _data.TryGetValue(key, out value);
        }

        public void RemoveData(string key)
        {
            if (_data.Remove(key))
            {
                _ = _isPlayerMetadata ?
                    _service.SetPlayerMetadataAsync(_lobbyId, _data) :
                    _service.SetMetadataAsync(_lobbyId, _data);
            }
        }

        public event Action<string, string> onDataChanged;
        static readonly List<string> _tempKeys = new ();

        internal void Update(Dictionary<string, string> metadata)
        {
            foreach (var kvp in metadata)
            {
                if (_data.TryGetValue(kvp.Key, out var existingValue))
                {
                    if (existingValue != kvp.Value)
                    {
                        _data[kvp.Key] = kvp.Value;
                        onDataChanged?.Invoke(kvp.Key, kvp.Value);
                    }
                }
                else
                {
                    _data[kvp.Key] = kvp.Value;
                    onDataChanged?.Invoke(kvp.Key, kvp.Value);
                }
            }

            // Check for removed keys
            _tempKeys.Clear();

            foreach (var key in _data.Keys)
            {
                if (!metadata.ContainsKey(key))
                    _tempKeys.Add(key);
            }

            for (var i = 0; i < _tempKeys.Count; i++)
            {
                var key = _tempKeys[i];
                _data.Remove(key);
                onDataChanged?.Invoke(key, null);
            }

            _tempKeys.Clear();
        }
    }
}
