using System;
using PurrNet.Services.Telemetry;
using PurrNet.Utils;
using UnityEditor;
using UnityEngine;

namespace PurrNet.Services.Editor
{
    internal static class PurrServicesProjectLink
    {
        static string LinkedProjectIdKey => "PurrServices_LinkedProjectId_" + Application.dataPath;
        static string LinkedProjectNameKey => "PurrServices_LinkedProjectName_" + Application.dataPath;

        internal static string projectId => EditorPrefs.GetString(LinkedProjectIdKey, PurrTelemetrySettings.projectId ?? "");
        internal static string projectName => EditorPrefs.GetString(LinkedProjectNameKey, PurrTelemetrySettings.projectName ?? "");
        internal static string publicKey => PurrTelemetrySettings.publicKey;

        internal static void Link(ProjectInfo project)
        {
            if (project == null) return;

            EditorPrefs.SetString(LinkedProjectIdKey, project.id);
            EditorPrefs.SetString(LinkedProjectNameKey, project.name);

            SetConstant(PurrTelemetrySettings.KeyProjectId, project.id);
            SetConstant(PurrTelemetrySettings.KeyProjectName, project.name);
            SetConstant(PurrTelemetrySettings.KeyPublicKey, project.publicKey);
        }

        internal static void Unlink()
        {
            EditorPrefs.DeleteKey(LinkedProjectIdKey);
            EditorPrefs.DeleteKey(LinkedProjectNameKey);

            DeleteConstant(PurrTelemetrySettings.KeyProjectId);
            DeleteConstant(PurrTelemetrySettings.KeyProjectName);
            DeleteConstant(PurrTelemetrySettings.KeyPublicKey);
        }

        internal static ProjectInfo FindLinkedProject(ProjectInfo[] projects)
        {
            var linkedId = projectId;
            if (string.IsNullOrEmpty(linkedId) || projects == null)
                return null;

            return Array.Find(projects, project => project.id == linkedId);
        }

        static void SetConstant(string key, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                DeleteConstant(key);
                return;
            }

            if (!ApplicationConstants.TryGet(key, out var current) || current != value)
                ApplicationConstants.Set(key, value);
        }

        static void DeleteConstant(string key)
        {
            if (ApplicationConstants.TryGet(key, out _))
                ApplicationConstants.Delete(key);
        }
    }
}
