#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif
#if STEAMWORKS && !DISABLESTEAMWORKS
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Steamworks;

namespace PurrNet.Lobby.Steam
{
    /// <summary>
    /// Bridges Steam <see cref="CallResult{T}"/> callbacks into awaitable Tasks.
    /// In-flight call results are kept alive in a static set so they are never
    /// garbage-collected before Steam answers, and every call carries a timeout
    /// failsafe so an unanswered call cannot hang a provider forever.
    /// </summary>
    internal static class SteamCallResults
    {
        private const int DefaultTimeoutMs = 30_000;

        private static readonly HashSet<IDisposable> _inFlight = new();

        public static async Task<T> ToTask<T>(SteamAPICall_t call, int timeoutMs = DefaultTimeoutMs)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            CallResult<T> callResult = null;
            callResult = CallResult<T>.Create((result, ioFailure) =>
            {
                _inFlight.Remove(callResult);
                callResult.Dispose();

                if (ioFailure)
                    tcs.TrySetException(new Exception($"Steam call {typeof(T).Name} failed with an IO error."));
                else
                    tcs.TrySetResult(result);
            });

            _inFlight.Add(callResult);
            callResult.Set(call);

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
            if (completed != tcs.Task)
            {
                _inFlight.Remove(callResult);
                callResult.Dispose();
                throw new TimeoutException($"Steam call {typeof(T).Name} timed out after {timeoutMs / 1000}s.");
            }

            return await tcs.Task;
        }
    }
}
#endif
