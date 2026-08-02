using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PurrNet.Editor;
using UnityEditor;
using UnityEngine;

namespace PurrNet.Lobby.Editor
{
    [CustomEditor(typeof(LobbyManager), true)]
    public sealed class LobbyManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var orchestratorProperty = serializedObject.FindProperty("_orchestrator");
            if (orchestratorProperty?.objectReferenceValue is GameOrchestrator orchestrator)
            {
                ProviderDependencyInspector.DrawMissingDependencies(orchestrator);
                PurrServicesSetupInspector.DrawIfNeeded(orchestrator);
            }
        }
    }

    internal static class ProviderDependencyInspector
    {
        private sealed class MissingDependency
        {
            public ProviderDependencyAttribute dependency;
            public readonly List<string> consumers = new();
        }

        public static void DrawMissingDependencies(GameOrchestrator orchestrator)
        {
            var missing = CollectMissingDependencies(orchestrator);
            if (missing.Count == 0)
                return;

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Missing Provider Dependencies", EditorStyles.boldLabel);

            foreach (var item in missing)
            {
                var warning = $"{item.dependency.displayName} is required by:\n" +
                              string.Join("\n", item.consumers.Select(consumer => $"• {consumer}"));

                PurrPackageQuickInstall.DrawInstallControls(
                    item.dependency.packageName,
                    item.dependency.displayName,
                    warning);
            }
        }

        private static List<MissingDependency> CollectMissingDependencies(GameOrchestrator orchestrator)
        {
            var missingByPackage = new Dictionary<string, MissingDependency>(StringComparer.OrdinalIgnoreCase);

            AddProviderDependencies(missingByPackage, "Session Provider", orchestrator.sessionProvider);
            AddProviderDependencies(missingByPackage, "Lobby Provider", orchestrator.lobbyProvider);
            AddProviderDependencies(missingByPackage, "Matchmaking Provider", orchestrator.matchmakingProvider);
            AddProviderDependencies(missingByPackage, "Game Allocator", orchestrator.gameAllocator);

            return missingByPackage.Values
                .OrderBy(item => item.dependency.displayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddProviderDependencies(
            IDictionary<string, MissingDependency> missingByPackage,
            string slotName,
            ScriptableObject provider)
        {
            if (!provider)
                return;

            var dependencies = provider.GetType()
                .GetCustomAttributes(typeof(ProviderDependencyAttribute), true)
                .Cast<ProviderDependencyAttribute>();

            foreach (var dependency in dependencies)
            {
                if (string.IsNullOrWhiteSpace(dependency.packageName) ||
                    PurrPackageQuickInstall.IsInstalled(dependency.packageName))
                    continue;

                if (!missingByPackage.TryGetValue(dependency.packageName, out var missing))
                {
                    missing = new MissingDependency { dependency = dependency };
                    missingByPackage.Add(dependency.packageName, missing);
                }

                var consumer = $"{slotName}: {provider.name}";
                if (!missing.consumers.Contains(consumer))
                    missing.consumers.Add(consumer);
            }
        }
    }

    internal static class PurrServicesSetupInspector
    {
        private const string PackageName = "dev.purrnet.services";
        private const string SetupMenuPath = "Tools/PurrNet/PurrServices";
        private const string SettingsTypeName =
            "PurrNet.Services.PurrServicesSettings, PurrServices.Runtime";
        private const string SetupWindowTypeName =
            "PurrNet.Services.Editor.PurrServicesSetupWindow, PurrServices.Editor";

        public static void DrawIfNeeded(GameOrchestrator orchestrator)
        {
            if (!PurrPackageQuickInstall.IsInstalled(PackageName) ||
                !UsesPackage(orchestrator, PackageName) ||
                !TryGetActiveConfiguration(out var projectId, out var apiKey, out var isEditorProfile) ||
                (!string.IsNullOrWhiteSpace(projectId) && !string.IsNullOrWhiteSpace(apiKey)))
            {
                return;
            }

            var profileName = isEditorProfile
                ? "Unity Editor override"
                : "Player Builds profile (currently also used by Play Mode)";

            GUILayout.Space(10);
            EditorGUILayout.LabelField("PurrServices Setup Required", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"PurrServices is selected by this orchestrator, but its {profileName} is not linked " +
                "to a project. Authentication will fail at runtime. Create or select a project in " +
                "PurrServices, then assign it to Player Builds or enable and assign the Unity Editor override.",
                MessageType.Error);

            var projectName = GetUnityProjectName();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"New Project ({projectName})…"))
                    OpenSetupWindow(projectName);

                if (GUILayout.Button("Open PurrServices"))
                    OpenSetupWindow();
            }
        }

        private static bool UsesPackage(GameOrchestrator orchestrator, string packageName)
        {
            return Providers(orchestrator).Any(provider => provider && provider.GetType()
                .GetCustomAttributes(typeof(ProviderDependencyAttribute), true)
                .Cast<ProviderDependencyAttribute>()
                .Any(dependency => string.Equals(
                    dependency.packageName,
                    packageName,
                    StringComparison.OrdinalIgnoreCase)));
        }

        private static IEnumerable<ScriptableObject> Providers(GameOrchestrator orchestrator)
        {
            yield return orchestrator.sessionProvider;
            yield return orchestrator.lobbyProvider;
            yield return orchestrator.matchmakingProvider;
            yield return orchestrator.gameAllocator;
        }

        private static bool TryGetActiveConfiguration(
            out string projectId,
            out string apiKey,
            out bool isEditorProfile)
        {
            projectId = null;
            apiKey = null;
            isEditorProfile = false;

            var settingsType = Type.GetType(SettingsTypeName);
            if (settingsType == null)
                return false;

            try
            {
                projectId = settingsType.GetProperty("projectId")?.GetValue(null) as string;
                apiKey = settingsType.GetProperty("apiKey")?.GetValue(null) as string;
                isEditorProfile = settingsType.GetProperty("isEditorProfile")?.GetValue(null) is true;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        private static string GetUnityProjectName()
        {
            var projectDirectory = Directory.GetParent(Application.dataPath);
            return projectDirectory?.Name ?? Application.productName ?? "Unity Project";
        }

        private static void OpenSetupWindow(string newProjectName = null)
        {
            if (!string.IsNullOrWhiteSpace(newProjectName))
            {
                var windowType = Type.GetType(SetupWindowTypeName);
                var showCreateProject = windowType?.GetMethod(
                    "ShowCreateProject",
                    new[] { typeof(string) });
                if (showCreateProject != null)
                {
                    try
                    {
                        showCreateProject.Invoke(null, new object[] { newProjectName });
                        return;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }

            if (!EditorApplication.ExecuteMenuItem(SetupMenuPath))
            {
                Debug.LogError($"Could not open {SetupMenuPath}. Reinstall or update PurrServices.");
            }
        }
    }
}
