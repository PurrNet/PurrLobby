using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace PurrNet.Lobby.PurrNet
{
    [Serializable]
    public class MatchmakingRegionProbe
    {
        public string region;
        public string url;

        /// <summary>Measure actual RTT without sending project or session credentials.
        /// Endpoints must support GET and WebGL CORS. Failed regions are omitted.</summary>
        public static async Task<Dictionary<string, float>> MeasureAsync(MatchmakingRegionProbe[] probes, CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, float>();
            if (probes == null || probes.Length > 8) return result;
            var measurements = new List<Task<KeyValuePair<string, float>?>>();
            foreach (var probe in probes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (probe == null || string.IsNullOrWhiteSpace(probe.region)
                    || !Uri.TryCreate(probe.url, UriKind.Absolute, out var uri)
                    || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback))) continue;
                measurements.Add(MeasureRegionAsync(probe, cancellationToken));
            }
            foreach (var measurement in await Task.WhenAll(measurements))
                if (measurement.HasValue) result[measurement.Value.Key] = measurement.Value.Value;
            return result;
        }

        private static async Task<KeyValuePair<string, float>?> MeasureRegionAsync(MatchmakingRegionProbe probe, CancellationToken cancellationToken)
        {
            float best = float.PositiveInfinity;
            for (int sample = 0; sample < 2; sample++)
            {
                using var request = UnityWebRequest.Get(probe.url);
                request.timeout = 3;
                var stopwatch = Stopwatch.StartNew();
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (cancellationToken.IsCancellationRequested) { request.Abort(); cancellationToken.ThrowIfCancellationRequested(); }
                    await Task.Yield();
                }
                stopwatch.Stop();
                cancellationToken.ThrowIfCancellationRequested();
                if (request.result == UnityWebRequest.Result.Success)
                    best = Math.Min(best, (float)stopwatch.Elapsed.TotalMilliseconds);
            }
            return float.IsPositiveInfinity(best) ? (KeyValuePair<string, float>?)null
                : new KeyValuePair<string, float>(probe.region, best);
        }
    }
}
