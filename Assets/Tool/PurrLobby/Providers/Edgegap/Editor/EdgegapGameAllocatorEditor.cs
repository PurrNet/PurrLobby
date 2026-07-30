using PurrNet.Editor;
using UnityEditor;
using UnityEngine;

namespace PurrNet.Lobby.Edgegap
{
    [CustomEditor(typeof(EdgegapGameAllocator), true)]
    public sealed class EdgegapGameAllocatorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

#if !PURR_SERVICES
            GUILayout.Space(10);
            PurrPackageQuickInstall.DrawInstallControls(
                "dev.purrnet.services",
                "PurrServices",
                "PurrServices is not installed. Install it to use direct Edgegap allocation.");
#endif
        }
    }
}
