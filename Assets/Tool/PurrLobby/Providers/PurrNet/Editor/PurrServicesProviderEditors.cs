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
}
