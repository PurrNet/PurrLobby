using System;
using System.Collections.Generic;
using UnityEngine;

namespace PurrNet.Lobby
{
    /// <summary>
    /// Shared <see cref="IMetadata"/> implementation with the standardized semantics:
    /// local writes fire <see cref="onDataChanged"/> immediately (optimistic echo) and
    /// then replicate through <see cref="PushChange"/>; inbound remote state is applied
    /// via <see cref="ReplaceFrom"/>/<see cref="ApplyPatch"/> which fire per-key diffs.
    /// A null value always means removal.
    /// </summary>
    public abstract class LobbyMetadataBase : IMetadata
    {
        protected readonly Dictionary<string, string> data = new();

        private readonly List<string> _scratchKeys = new();

        public event Action<string, string> onDataChanged;

        /// <summary>Whether the local client is allowed to write. Rejected writes warn and no-op.</summary>
        protected virtual bool CanWrite => true;

        /// <summary>Replicates a single change to the backend. A null value means the key was removed.</summary>
        protected abstract void PushChange(string key, string value);

        public void SetData(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (data.TryGetValue(key, out var existing) && existing == value)
                return;

            if (!CanWrite)
            {
                Debug.LogWarning($"[{GetType().Name}] This metadata is read-only for the local client.");
                return;
            }

            data[key] = value;
            onDataChanged?.Invoke(key, value);
            PushChange(key, value);
        }

        public void RemoveData(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (!data.ContainsKey(key))
                return;

            if (!CanWrite)
            {
                Debug.LogWarning($"[{GetType().Name}] This metadata is read-only for the local client.");
                return;
            }

            data.Remove(key);
            onDataChanged?.Invoke(key, null);
            PushChange(key, null);
        }

        public string GetData(string key) => data.GetValueOrDefault(key);

        public bool TryGetData(string key, out string value) => data.TryGetValue(key, out value);

        public bool ContainsData(string key) => data.ContainsKey(key);

        /// <summary>The current data. Do not mutate; copy before storing.</summary>
        public IReadOnlyDictionary<string, string> Snapshot() => data;

        /// <summary>Replaces all data with the given state and fires <see cref="onDataChanged"/> for any differences.</summary>
        public void ReplaceFrom(IReadOnlyDictionary<string, string> replacement)
        {
            _scratchKeys.Clear();
            foreach (var key in data.Keys)
            {
                if (replacement == null || !replacement.ContainsKey(key))
                    _scratchKeys.Add(key);
            }
            for (int i = 0; i < _scratchKeys.Count; i++)
            {
                data.Remove(_scratchKeys[i]);
                onDataChanged?.Invoke(_scratchKeys[i], null);
            }
            _scratchKeys.Clear();

            if (replacement == null)
                return;

            foreach (var kvp in replacement)
            {
                if (data.TryGetValue(kvp.Key, out var existing) && existing == kvp.Value)
                    continue;
                data[kvp.Key] = kvp.Value;
                onDataChanged?.Invoke(kvp.Key, kvp.Value);
            }
        }

        /// <summary>Applies a partial patch. Null values delete the key.</summary>
        public void ApplyPatch(IReadOnlyDictionary<string, string> patch)
        {
            if (patch == null)
                return;

            foreach (var kvp in patch)
            {
                if (kvp.Value == null)
                {
                    if (data.Remove(kvp.Key))
                        onDataChanged?.Invoke(kvp.Key, null);
                }
                else
                {
                    if (data.TryGetValue(kvp.Key, out var existing) && existing == kvp.Value)
                        continue;
                    data[kvp.Key] = kvp.Value;
                    onDataChanged?.Invoke(kvp.Key, kvp.Value);
                }
            }
        }
    }
}
