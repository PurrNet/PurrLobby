using UnityEditor;
using UnityEngine;
#if PURR_SERVICES
using PurrNet.Services;
#endif

namespace PurrNet.Lobby.Editor
{
    [CustomEditor(typeof(LobbyManager))]
    public sealed class LobbyManagerEditor : UnityEditor.Editor
    {
#if PURR_SERVICES
        private const string PurrNetSessionProviderType =
            "PurrNet.Lobby.PurrNet.PurrNetSessionProvider";

        private const string PurrNetLobbyProviderType =
            "PurrNet.Lobby.PurrNet.PurrNetLobbyProvider";

        private const string EdgegapGameAllocatorType =
            "PurrNet.Lobby.Edgegap.EdgegapGameAllocator";
#endif

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

#if PURR_SERVICES
            var orchestratorProperty = serializedObject.FindProperty("_orchestrator");
            var orchestrator = orchestratorProperty?.objectReferenceValue as GameOrchestrator;

            if (orchestrator == null ||
                !UsesPurrServices(orchestrator) ||
                !PurrServicesSettings.isFreeTier)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "This Lobby Manager is configured to use the PurrServices free tier.\n" +
                "The free tier is for development use only and must not be used in production. " +
                "Configure a PurrServices project before shipping.",
                MessageType.Warning);
#endif
        }

#if PURR_SERVICES
        private static bool UsesPurrServices(GameOrchestrator orchestrator)
        {
            if (IsTypeOrSubclass(orchestrator.sessionProvider, PurrNetSessionProviderType) ||
                IsTypeOrSubclass(orchestrator.lobbyProvider, PurrNetLobbyProviderType))
            {
                return true;
            }

            // Edgegap matchmaking supplies a ready connection and bypasses direct
            // allocation. A lobby flow calls AllocateGame(ILobby), which does use
            // PurrServices to deploy the game server.
            return orchestrator.lobbyProvider != null &&
                   IsTypeOrSubclass(orchestrator.gameAllocator, EdgegapGameAllocatorType);
        }

        private static bool IsTypeOrSubclass(Object value, string expectedTypeName)
        {
            for (var type = value ? value.GetType() : null; type != null; type = type.BaseType)
            {
                if (type.FullName == expectedTypeName)
                    return true;
            }

            return false;
        }
#endif
    }
}
