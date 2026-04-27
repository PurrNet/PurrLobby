using System;
using System.Linq;
using PurrNet.Editor;
using UnityEditor;
using UnityEngine;

namespace PurrNet.Services.Editor
{
    [CustomEditor(typeof(PurrServices))]
    public class PurrServicesEditor : UnityEditor.Editor
    {
        static string LinkedProjectIdKey => "PurrServices_LinkedProjectId_" + Application.dataPath;

        SerializedProperty _serverUrl;
        SerializedProperty _apiKey;

        ProjectInfo[] _projects;
        string[] _dropdownNames;
        bool _isFetching;

        void OnEnable()
        {
            _serverUrl = serializedObject.FindProperty("_serverUrl");
            _apiKey = serializedObject.FindProperty("_apiKey");

            if (string.IsNullOrEmpty(_apiKey.stringValue) && _cachedLinkedKey != null)
            {
                _apiKey.stringValue = _cachedLinkedKey;
                serializedObject.ApplyModifiedProperties();
            }

            if (PurrPackageManagerAuth.HasApiKey())
                FetchProjects();
        }

        async void FetchProjects()
        {
            if (_isFetching) return;
            _isFetching = true;

            try
            {
                var result = await PurrServicesAPI.GetProjects(PurrPackageManagerAuth.GetApiKey());
                if (result.Success && result.Value.projects != null)
                {
                    _projects = result.Value.projects;
                    _dropdownNames = new[] { "—" }
                        .Concat(_projects.Select(p => p.name))
                        .ToArray();
                }
            }
            catch
            {
                _projects = null;
                _dropdownNames = null;
            }
            finally
            {
                _isFetching = false;
                Repaint();
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_serverUrl);
            EditorGUILayout.PropertyField(_apiKey);

            EditorGUILayout.BeginHorizontal();

            if (_projects != null && _projects.Length > 0)
            {
                int current = 0;
                for (int i = 0; i < _projects.Length; i++)
                {
                    if (_projects[i].publicKey == _apiKey.stringValue)
                    {
                        current = i + 1;
                        break;
                    }
                }

                var selected = EditorGUILayout.Popup("Project", current, _dropdownNames);
                if (selected != current && selected > 0 && !string.IsNullOrEmpty(_projects[selected - 1].publicKey))
                    _apiKey.stringValue = _projects[selected - 1].publicKey;
            }
            else
            {
                GUI.enabled = false;
                EditorGUILayout.Popup("Project", 0, new[] { "—" });
                GUI.enabled = true;
            }

            if (GUILayout.Button("...", GUILayout.Width(22)))
                PurrServicesSetupWindow.ShowWindow();

            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        public static string GetLinkedPublicKey()
        {
            var linkedId = EditorPrefs.GetString("PurrServices_LinkedProjectId_" + Application.dataPath, "");
            if (string.IsNullOrEmpty(linkedId) || !PurrPackageManagerAuth.HasApiKey())
                return null;

            return _cachedLinkedKey;
        }

        static string _cachedLinkedKey;

        [InitializeOnLoadMethod]
        static void CacheLinkedKey()
        {
            PurrPackageManagerAuth.onAuthChanged += RefreshCache;
            RefreshCache();
        }

        static async void RefreshCache()
        {
            _cachedLinkedKey = null;
            if (!PurrPackageManagerAuth.HasApiKey()) return;

            var linkedId = EditorPrefs.GetString("PurrServices_LinkedProjectId_" + Application.dataPath, "");
            if (string.IsNullOrEmpty(linkedId)) return;

            try
            {
                var result = await PurrServicesAPI.GetProjects(PurrPackageManagerAuth.GetApiKey());
                if (!result.Success) return;

                var linked = Array.Find(result.Value.projects, p => p.id == linkedId);
                if (linked != null)
                    _cachedLinkedKey = linked.publicKey;
            }
            catch
            {
                // ignore
            }
        }
    }
}
