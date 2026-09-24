#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif
using PurrNet.Editor;
using UnityEditor;
using UnityEngine;

namespace PurrNet.Lobby.PurrNet.Editor
{
    internal static class PurrServicesInstallPrompt
    {
        private const string PackageName = "dev.purrnet.services";

        public static void Draw(string warning)
        {
            GUILayout.Space(10);
            PurrPackageQuickInstall.DrawInstallControls(
                PackageName,
                "PurrServices",
                warning);
        }
    }

    [CustomEditor(typeof(PurrNetLobbyProvider), true)]
    public sealed class PurrNetLobbyProviderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

#if !PURR_SERVICES
            PurrServicesInstallPrompt.Draw(
                "PurrServices is not installed. Install it to use this lobby provider.");
#endif
        }
    }

    [CustomEditor(typeof(PurrNetSessionProvider), true)]
    public sealed class PurrNetSessionProviderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

#if !PURR_SERVICES
            PurrServicesInstallPrompt.Draw(
                "PurrServices is not installed. Install it to use this session provider.");
#endif
        }
    }

    [CustomEditor(typeof(PurrNetSteamSessionProvider), true)]
    public sealed class PurrNetSteamSessionProviderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

#if !PURR_SERVICES
            PurrServicesInstallPrompt.Draw(
                "PurrServices is not installed. Install it to use this session provider.");
#elif !PURR_SERVICES_STEAM
            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Steam sign-in needs PurrServices 1.2.1 or newer. Update it in the Package Manager.",
                MessageType.Warning);
            if (GUILayout.Button("Open Package Manager"))
                PurrPackageQuickInstall.OpenPackagesWindow();
#elif !STEAMWORKS
            GUILayout.Space(10);
            PurrPackageQuickInstall.DrawInstallControls(
                "com.rlabrecque.steamworks.net",
                "Steamworks.NET",
                "Steamworks.NET is not installed. Install it to use Steam sign-in.");
#elif DISABLESTEAMWORKS
            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Steam is only supported on Windows, Linux, and macOS standalone targets.",
                MessageType.Info);
#else
            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Players sign in to PurrServices with their Steam account (player id steam:<steamid64>). " +
                "Pair this with the PurrNet lobby provider.\n\n" +
                "On purrnet.dev open your project → Auth → Steam, choose a mode and switch Steam sign-in on:\n" +
                "• Verify with Steam: enter your App ID and a publisher Web API key. Valve's test app 480 " +
                "cannot be verified, so put your own App ID in steam_appid.txt.\n" +
                "• Trust the game: no setup, but anyone with a modified client can sign in as any Steam " +
                "account. Fine while starting out; switch to verifying before it matters.",
                MessageType.Info);
#endif
        }
    }
}
