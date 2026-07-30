#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif
using PurrNet.Editor;
using UnityEditor;
using UnityEngine;

namespace PurrNet.Lobby.Steam.Editor
{
    internal static class SteamDependencyPrompt
    {
        public static void Draw()
        {
#if DISABLESTEAMWORKS
            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Steam is only supported on Windows, Linux, and macOS standalone targets.",
                MessageType.Info);
#elif !STEAMWORKS
            GUILayout.Space(10);
            PurrPackageQuickInstall.DrawInstallControls(
                "com.rlabrecque.steamworks.net",
                "Steamworks.NET",
                "Steamworks.NET is not installed. Install it to use this Steam provider.");
#else
            SteamSetupUtility.DrawAppIdSetup();
#endif
        }
    }

    [CustomEditor(typeof(SteamLobbyProvider), true)]
    public sealed class SteamLobbyProviderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            SteamDependencyPrompt.Draw();
        }
    }

    [CustomEditor(typeof(SteamSessionProvider), true)]
    public sealed class SteamSessionProviderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            SteamDependencyPrompt.Draw();
        }
    }

    [CustomEditor(typeof(SteamGameAllocator), true)]
    public sealed class SteamGameAllocatorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            SteamDependencyPrompt.Draw();
        }
    }
}
