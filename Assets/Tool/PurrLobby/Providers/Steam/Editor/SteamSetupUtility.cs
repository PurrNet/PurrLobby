using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace PurrNet.Lobby.Steam.Editor
{
    /// <summary>
    /// Setup helpers for the Steam lobby provider: installing Steamworks.NET and
    /// creating the steam_appid.txt Steam requires when launching from the editor.
    /// </summary>
    public static class SteamSetupUtility
    {
        private const string SteamworksPackageUrl =
            "https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2024.8.0";

        private const string TestAppId = "480"; // Spacewar, Valve's public test AppID.

        [MenuItem("Tools/PurrLobby/Steam/Install Steamworks.NET")]
        public static void InstallSteamworks()
        {
#if STEAMWORKS
            EditorUtility.DisplayDialog("Steamworks.NET", "Steamworks.NET is already installed.", "OK");
#else
            Client.Add(SteamworksPackageUrl);
            Debug.Log("[PurrLobby] Installing Steamworks.NET via the Package Manager...");
#endif
        }

        [MenuItem("Tools/PurrLobby/Steam/Create steam_appid.txt (480)")]
        public static void CreateTestAppId()
        {
            var path = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "steam_appid.txt");

            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path).Trim();
                if (!EditorUtility.DisplayDialog("steam_appid.txt",
                        $"steam_appid.txt already exists with AppID {existing}. Overwrite with {TestAppId} (Spacewar)?",
                        "Overwrite", "Cancel"))
                    return;
            }

            File.WriteAllText(path, TestAppId);
            Debug.Log($"[PurrLobby] Wrote {path} with test AppID {TestAppId} (Spacewar). " +
                      "Replace it with your own AppID before shipping.");
        }
    }
}
