using System;
using System.Collections.Generic;

namespace PurrLobby
{
    internal sealed class PurrMetadata : IMetadata
    {
        readonly Dictionary<string, string> _data = new Dictionary<string, string>();

        /// <summary>
        /// Called by the lobby when it wants to send a metadata patch to the server.
        /// The lobby sets this after creation.
        /// </summary>
        internal Action<Dictionary<string, string>> onPatchRequested;

        public event Action<string, string> onDataChanged;

        public void SetData(string key, string value)
        {
            if (_data.TryGetValue(key, out var existing) && existing == value)
                return;

            _data[key] = value;
            onDataChanged?.Invoke(key, value);
            onPatchRequested?.Invoke(new Dictionary<string, string> { { key, value } });
        }

        public string GetData(string key)
        {
            return _data.TryGetValue(key, out var val) ? val : null;
        }

        public bool TryGetData(string key, out string value)
        {
            return _data.TryGetValue(key, out value);
        }

        public void RemoveData(string key)
        {
            if (_data.Remove(key))
            {
                onDataChanged?.Invoke(key, null);
                // Server convention: empty string removes the key
                onPatchRequested?.Invoke(new Dictionary<string, string> { { key, "" } });
            }
        }

        /// <summary>
        /// Apply a full snapshot from the server, firing events for changes.
        /// </summary>
        internal void ApplySnapshot(Dictionary<string, string> snapshot)
        {
            if (snapshot == null) snapshot = new Dictionary<string, string>();

            // Detect removed keys
            var toRemove = new List<string>();
            foreach (var kv in _data)
            {
                if (!snapshot.ContainsKey(kv.Key))
                    toRemove.Add(kv.Key);
            }

            foreach (var key in toRemove)
            {
                _data.Remove(key);
                onDataChanged?.Invoke(key, null);
            }

            // Detect added/changed keys
            foreach (var kv in snapshot)
            {
                if (!_data.TryGetValue(kv.Key, out var existing) || existing != kv.Value)
                {
                    _data[kv.Key] = kv.Value;
                    onDataChanged?.Invoke(kv.Key, kv.Value);
                }
            }
        }

        /// <summary>
        /// Replace all data silently (no events). Used for initial population.
        /// </summary>
        internal void SetAllSilent(Dictionary<string, string> data)
        {
            _data.Clear();
            if (data != null)
            {
                foreach (var kv in data)
                    _data[kv.Key] = kv.Value;
            }
        }
    }
}
