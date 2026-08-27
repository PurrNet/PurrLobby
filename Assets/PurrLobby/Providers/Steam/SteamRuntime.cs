#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif
#if STEAMWORKS && !DISABLESTEAMWORKS
using Steamworks;
using UnityEngine;

namespace PurrNet.Lobby.Steam
{
    /// <summary>
    /// Bootstraps the Steamworks API for the lobby providers: initializes SteamAPI
    /// once, pumps callbacks every frame via a hidden GameObject, and shuts down on
    /// application quit. Safe to call from any provider at any time.
    /// </summary>
    public static class SteamRuntime
    {
        public static bool isInitialized { get; private set; }

        private static bool _initFailed;
        private static GameObject _pump;

        public static CSteamID localSteamId => SteamUser.GetSteamID();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            isInitialized = false;
            _initFailed = false;
            _pump = null;
        }

        /// <summary>
        /// Idempotent. Returns false (and logs once) when Steam isn't running or
        /// steam_appid.txt is missing — providers then fail gracefully.
        /// </summary>
        public static bool EnsureInitialized()
        {
            if (isInitialized)
                return true;

            if (_initFailed)
                return false;

            if (!Packsize.Test() || !DllCheck.Test())
            {
                _initFailed = true;
                Debug.LogError("[SteamRuntime] Steamworks.NET plugin mismatch. Reimport the Steamworks.NET package.");
                return false;
            }

            if (!SteamAPI.Init())
            {
                _initFailed = true;
                Debug.LogError("[SteamRuntime] SteamAPI.Init() failed. Is the Steam client running, " +
                               "and is steam_appid.txt present next to the executable (or project root in the editor)?");
                return false;
            }

            isInitialized = true;

            _pump = new GameObject("PurrLobby Steam Pump");
            _pump.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(_pump);
            _pump.AddComponent<SteamCallbackPump>();

            return true;
        }

        internal static void Shutdown()
        {
            if (!isInitialized)
                return;
            isInitialized = false;

            // HideAndDontSave objects survive exiting play mode in the editor;
            // destroy explicitly so pumps don't accumulate across sessions.
            if (_pump)
                Object.Destroy(_pump);
            _pump = null;

            SteamAPI.Shutdown();
        }
    }

    internal class SteamCallbackPump : MonoBehaviour
    {
        private void Update()
        {
            SteamAPI.RunCallbacks();
        }

        private void OnApplicationQuit()
        {
            SteamRuntime.Shutdown();
        }
    }
}
#endif
