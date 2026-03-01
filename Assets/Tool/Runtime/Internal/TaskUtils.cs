using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace PurrLobby.Internal
{
    internal static class TaskUtils
    {
        public static Task DelaySeconds(float seconds) => PurrRunner.DelaySeconds(seconds);

        public static async void Forget(this Task task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    internal sealed class PurrRunner : MonoBehaviour
    {
        static PurrRunner _instance;

        List<TaskCompletionSource<bool>> _pending = new();
        List<TaskCompletionSource<bool>> _processing = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            if (_instance != null) return;
            var go = new GameObject("[PurrLobby]");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _instance = go.AddComponent<PurrRunner>();
        }

        static PurrRunner instance
        {
            get
            {
                if (_instance == null) Initialize();
                return _instance;
            }
        }

        void Update()
        {
            if (_pending.Count == 0) return;

            (_pending, _processing) = (_processing, _pending);

            foreach (var tcs in _processing)
                tcs.TrySetResult(true);

            _processing.Clear();
        }

        public static Task YieldAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            instance._pending.Add(tcs);
            return tcs.Task;
        }

        public static async Task DelaySeconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
                await YieldAsync();
        }
    }
}
