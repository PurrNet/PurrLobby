using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PurrNet.Editor;
using PurrNet.Utils;
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
        private const string ServicesApiTypeName =
            "PurrNet.Services.Editor.PurrServicesAPI, PurrServices.Editor";

        private static bool _isCreatingProject;

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
                using (new EditorGUI.DisabledScope(
                           _isCreatingProject || EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    var createLabel = _isCreatingProject
                        ? $"Creating {projectName}…"
                        : $"Create & Link ({projectName})";
                    if (GUILayout.Button(createLabel))
                        CreateAndLinkProject(projectName, isEditorProfile);
                }

                if (GUILayout.Button("Open PurrServices"))
                    OpenSetupWindow();
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox(
                    "Exit Play Mode before changing the linked PurrServices project.",
                    MessageType.Info);
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

        private static async void CreateAndLinkProject(string projectName, bool editorProfile)
        {
            if (_isCreatingProject)
                return;

            var windowType = Type.GetType(SetupWindowTypeName);
            var createAndLink = windowType?.GetMethod(
                "CreateAndLinkProject",
                new[] { typeof(string), typeof(bool) });

            _isCreatingProject = true;
            RepaintAllViews();
            try
            {
                var result = createAndLink == null
                    ? await CreateAndLinkLegacyProject(projectName, editorProfile)
                    : await InvokeCreateAndLink(createAndLink, projectName, editorProfile);
                if (!result.Success)
                {
                    ShowQuickSetupFailure(result.Error, projectName);
                    return;
                }

                var profileName = editorProfile ? "Unity Editor" : "Player Builds";
                Debug.Log($"[PurrLobby] Created PurrServices project '{projectName}' and linked it to {profileName}.");
            }
            catch (Exception exception)
            {
                ShowQuickSetupFailure(exception.GetBaseException().Message, projectName);
            }
            finally
            {
                _isCreatingProject = false;
                RepaintAllViews();
            }
        }

        private static async Task<Result<bool>> InvokeCreateAndLink(
            System.Reflection.MethodInfo createAndLink,
            string projectName,
            bool editorProfile)
        {
            var invocation = createAndLink.Invoke(
                null,
                new object[] { projectName, editorProfile });
            return invocation is Task<Result<bool>> task
                ? await task
                : Result<bool>.Fail(
                    "The installed PurrServices version returned an unsupported setup result.");
        }

        /// <summary>
        /// Compatibility for PurrServices versions predating CreateAndLinkProject.
        /// Uses their public create endpoint, then writes the same public settings
        /// keys as the package's project-link helper.
        /// </summary>
        private static async Task<Result<bool>> CreateAndLinkLegacyProject(
            string projectName,
            bool editorProfile)
        {
            if (!PurrPackageManagerAuth.HasApiKey())
            {
                return Result<bool>.Fail(
                    "Sign in to PurrNet before creating a PurrServices project.");
            }

            var apiType = Type.GetType(ServicesApiTypeName);
            var createProject = apiType?.GetMethod(
                "CreateProject",
                new[] { typeof(string), typeof(string) });
            if (createProject == null)
                return Result<bool>.Fail("Update PurrServices to use one-click project setup.");

            var invocation = createProject.Invoke(
                null,
                new object[] { PurrPackageManagerAuth.GetApiKey(), projectName });
            if (invocation is not Task task)
                return Result<bool>.Fail("PurrServices returned an unsupported project creation result.");

            await task;

            var apiResult = task.GetType().GetProperty("Result")?.GetValue(task);
            if (apiResult == null)
                return Result<bool>.Fail("PurrServices returned no project creation result.");

            var apiResultType = apiResult.GetType();
            var success = apiResultType.GetProperty("Success")?.GetValue(apiResult) is true;
            if (!success)
            {
                var error = apiResultType.GetProperty("Error")?.GetValue(apiResult) as string;
                return Result<bool>.Fail(error ?? "PurrServices could not create the project.");
            }

            var response = apiResultType.GetProperty("Value")?.GetValue(apiResult);
            var project = response?.GetType().GetField("project")?.GetValue(response);
            var projectType = project?.GetType();
            var projectId = projectType?.GetField("id")?.GetValue(project) as string;
            var linkedName = projectType?.GetField("name")?.GetValue(project) as string;
            var publicKey = projectType?.GetField("publicKey")?.GetValue(project) as string;
            if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(publicKey))
            {
                return Result<bool>.Fail(
                    "The project was created, but its runtime credentials were missing. " +
                    "Open PurrServices to finish linking it.");
            }

            var settingsType = Type.GetType(SettingsTypeName);
            var profilePrefix = editorProfile ? "Editor" : "Build";
            if (!TryGetSettingsKey(settingsType, $"Key{profilePrefix}ApiKey", out var apiKeyKey) ||
                !TryGetSettingsKey(settingsType, $"Key{profilePrefix}ProjectId", out var projectIdKey) ||
                !TryGetSettingsKey(settingsType, $"Key{profilePrefix}ProjectName", out var projectNameKey))
            {
                return Result<bool>.Fail(
                    "The project was created, but this PurrServices version does not expose its linking settings. " +
                    "Open PurrServices to finish linking it.");
            }

            ApplicationConstants.Set(apiKeyKey, publicKey);
            ApplicationConstants.Set(projectIdKey, projectId);
            ApplicationConstants.Set(projectNameKey, linkedName ?? projectName);
            return Result<bool>.Ok(true);
        }

        private static bool TryGetSettingsKey(Type settingsType, string fieldName, out string key)
        {
            key = settingsType?.GetField(fieldName)?.GetRawConstantValue() as string;
            return !string.IsNullOrWhiteSpace(key);
        }

        private static void ShowQuickSetupFailure(string error, string projectName)
        {
            var message = string.IsNullOrWhiteSpace(error)
                ? "PurrServices could not create the project."
                : error;
            var openSetup = EditorUtility.DisplayDialog(
                "PurrServices setup failed",
                message,
                "Open PurrServices",
                "Close");
            if (openSetup)
                OpenSetupWindow(projectName);
        }

        private static void RepaintAllViews()
        {
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
}
