using System.Threading.Tasks;
using UnityEngine;

namespace PurrNet.Lobby
{
    public static class TaskForgetExtensions
    {
        /// <summary>
        /// Intentionally fire-and-forget, but surface failures instead of
        /// swallowing them like a bare task discard does.
        /// </summary>
        public static void Forget(this Task task, string context = null)
        {
            if (task == null || task.IsCompletedSuccessfully)
                return;

            task.ContinueWith(t =>
            {
                if (t.Exception == null)
                    return;
                var error = t.Exception.GetBaseException();
                Debug.LogError(string.IsNullOrEmpty(context)
                    ? $"Fire-and-forget task failed: {error}"
                    : $"{context}: {error}");
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
