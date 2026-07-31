using System.IO;
using UnityEditor;
using UnityEngine;

namespace PurrNet.Lobby.Steam.Editor
{
    /// <summary>
    /// Setup helper for the steam_appid.txt Steam requires when launching from the editor.
    /// </summary>
    public static class SteamSetupUtility
    {
        private const string TestAppId = "480"; // Spacewar, Valve's public test AppID.

        private static string appIdPath =>
            Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "steam_appid.txt");

        internal static void DrawAppIdSetup()
        {
            if (File.Exists(appIdPath))
                return;

            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "steam_appid.txt is missing. Create a test file to run Steam from the editor.",
                MessageType.Warning);

            if (GUILayout.Button("Create steam_appid.txt (480 - Spacewar)"))
                CreateTestAppId();
        }

        private static void CreateTestAppId()
        {
            var path = appIdPath;

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
