using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace PurrNet.Services.Telemetry
{
    internal static class PurrTelemetrySender
    {
        const string EventsPath = "/api/services/telemetry/events";

        static readonly object _lock = new();
        static readonly List<EventPayload> _buffer = new();
        static readonly System.Random _random = new();

        static PurrTelemetryRunner _runner;
        static bool _flushInFlight;
        static bool _flushRequested;
        static bool _replayed;
        static bool _unauthorizedLogged;
        static int _mainThreadId = -1;
        static float _intervalAccumulator;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void CaptureMainThread()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RuntimeInit()
        {
            try
            {
                if (!PurrTelemetrySettings.isLinked) return;

                EnsureRunner();
                TryReplayPersisted();
            }
            catch (Exception e)
            {
                PurrTelemetry.LogIfEditor(e);
            }
        }

        public static void Enqueue(string eventName, IReadOnlyDictionary<string, object> props)
        {
            try
            {
                if (!PurrTelemetrySettings.isLinked) return;

                if (string.IsNullOrEmpty(eventName)) return;
                var trimmed = eventName.Trim();
                if (trimmed.Length == 0 || trimmed.Length > 128) return;

                var ev = new EventPayload
                {
                    EventName = trimmed,
                    Properties = CopyProperties(props),
                    Source = PurrTelemetry.CurrentSource,
                    OccurredAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
                };

                bool overThreshold;
                lock (_lock)
                {
                    _buffer.Add(ev);
                    overThreshold = _buffer.Count >= PurrTelemetrySettings.FlushBatchThreshold;
                }

                if (overThreshold)
                    _flushRequested = true;

                if (_runner == null && IsMainThread())
                    EnsureRunner();
            }
            catch (Exception e)
            {
                PurrTelemetry.LogIfEditor(e);
            }
        }

        static Dictionary<string, object> CopyProperties(IReadOnlyDictionary<string, object> props)
        {
            if (props == null || props.Count == 0) return null;
            var copy = new Dictionary<string, object>(props.Count);
            foreach (var kvp in props)
                copy[kvp.Key] = kvp.Value;
            return copy;
        }

        static bool IsMainThread() =>
            _mainThreadId == -1 || Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        static void EnsureRunner()
        {
            if (_runner != null) return;
            if (!Application.isPlaying) return;

            var go = new GameObject("[PurrTelemetry]") { hideFlags = HideFlags.HideInHierarchy };
            _runner = go.AddComponent<PurrTelemetryRunner>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        internal static void TickFromRunner(float deltaTime)
        {
            _intervalAccumulator += deltaTime;
            bool intervalElapsed = _intervalAccumulator >= PurrTelemetrySettings.FlushIntervalSeconds;

            int bufferCount;
            lock (_lock) bufferCount = _buffer.Count;

            if (_flushRequested || (intervalElapsed && bufferCount > 0))
            {
                _flushRequested = false;
                _intervalAccumulator = 0f;
                _ = FlushAsync();
            }
            else if (intervalElapsed)
            {
                _intervalAccumulator = 0f;
            }
        }

        public static async Task FlushAsync()
        {
            if (_flushInFlight) return;

            if (!PurrTelemetrySettings.isLinked) return;

            _flushInFlight = true;
            try
            {
                while (true)
                {
                    EventPayload[] batch;
                    lock (_lock)
                    {
                        if (_buffer.Count == 0) return;
                        int take = Math.Min(PurrTelemetrySettings.MaxBatchSize, _buffer.Count);
                        batch = new EventPayload[take];
                        _buffer.CopyTo(0, batch, 0, take);
                        _buffer.RemoveRange(0, take);
                    }

                    try
                    {
                        await SendBatchAsync(batch);
                    }
                    catch (Exception e)
                    {
                        PurrTelemetry.LogIfEditor(e);
                    }
                }
            }
            finally
            {
                _flushInFlight = false;
            }
        }

        static async Task SendBatchAsync(EventPayload[] batch)
        {
            var url = PurrTelemetrySettings.baseUrl.TrimEnd('/') + EventsPath;
            var publicKey = PurrTelemetrySettings.publicKey;

            byte[] bytes;
            try
            {
                var body = JsonConvert.SerializeObject(new BatchBody { Events = batch },
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                bytes = Encoding.UTF8.GetBytes(body);
            }
            catch (Exception e)
            {
                PurrTelemetry.LogIfEditor(e);
                return;
            }

            for (int attempt = 0; attempt < PurrTelemetrySettings.MaxRetries; attempt++)
            {
                using var req = new UnityWebRequest(url, "POST")
                {
                    uploadHandler = new UploadHandlerRaw(bytes),
                    downloadHandler = new DownloadHandlerBuffer()
                };
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {publicKey}");

                try
                {
                    await req.SendWebRequest();
                }
                catch
                {
                }

                int status = (int)req.responseCode;

                if (status >= 200 && status < 300)
                    return;

                if (status == 401)
                {
                    if (Application.isEditor && !_unauthorizedLogged)
                    {
                        Debug.LogWarning("[PurrTelemetry] 401 Unauthorized. Re-link the project from Tools/PurrNet/PurrServices.");
                        _unauthorizedLogged = true;
                    }
                    return;
                }

                if (status == 400 || status == 403)
                {
                    if (Application.isEditor)
                        Debug.LogWarning($"[PurrTelemetry] {status} dropping batch: {SafeBody(req)}");
                    return;
                }

                if (attempt + 1 < PurrTelemetrySettings.MaxRetries)
                {
                    int delayMs = ComputeBackoffMs(attempt);
                    await Task.Delay(delayMs);
                    continue;
                }

                if (Application.isEditor)
                    Debug.LogWarning($"[PurrTelemetry] Giving up after {PurrTelemetrySettings.MaxRetries} attempts (status {status}).");
                return;
            }
        }

        static int ComputeBackoffMs(int attempt)
        {
            int baseMs = 500 * (1 << Math.Min(attempt, 6));
            int jitter = _random.Next(-baseMs / 4, baseMs / 4);
            return Math.Max(100, baseMs + jitter);
        }

        static string SafeBody(UnityWebRequest req)
        {
            try { return req.downloadHandler?.text ?? req.error ?? ""; }
            catch { return ""; }
        }

        static string PersistencePath
        {
            get
            {
                var dir = Path.Combine(Application.persistentDataPath, "PurrTelemetry");
                return Path.Combine(dir, "pending.json");
            }
        }

        internal static void PersistPending()
        {
            EventPayload[] snapshot;
            lock (_lock)
            {
                if (_buffer.Count == 0) return;
                snapshot = _buffer.ToArray();
                _buffer.Clear();
            }

            try
            {
                var dir = Path.GetDirectoryName(PersistencePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var json = JsonConvert.SerializeObject(new BatchBody { Events = snapshot },
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                File.WriteAllText(PersistencePath, json);
            }
            catch (Exception e)
            {
                if (Application.isEditor)
                    Debug.LogWarning($"[PurrTelemetry] Failed to persist pending events: {e.Message}");
            }
        }

        static void TryReplayPersisted()
        {
            if (_replayed) return;
            _replayed = true;

            try
            {
                if (!File.Exists(PersistencePath)) return;
                var json = File.ReadAllText(PersistencePath);
                File.Delete(PersistencePath);

                if (string.IsNullOrEmpty(json)) return;

                var body = JsonConvert.DeserializeObject<BatchBody>(json);
                if (body?.Events == null || body.Events.Length == 0) return;

                lock (_lock) _buffer.InsertRange(0, body.Events);
                _flushRequested = true;
            }
            catch (Exception e)
            {
                if (Application.isEditor)
                    Debug.LogWarning($"[PurrTelemetry] Failed to replay persisted events: {e.Message}");
            }
        }

        [Serializable]
        internal class EventPayload
        {
            [JsonProperty("event_name")] public string EventName;
            [JsonProperty("properties", NullValueHandling = NullValueHandling.Ignore)] public Dictionary<string, object> Properties;
            [JsonProperty("source")] public string Source;
            [JsonProperty("occurred_at")] public string OccurredAt;
        }

        [Serializable]
        internal class BatchBody
        {
            [JsonProperty("events")] public EventPayload[] Events;
        }
    }
}
