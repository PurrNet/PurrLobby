using System;
using System.Linq;
using System.Text;
using PurrNet.Editor;
using PurrNet.Services.Telemetry;
using PurrNet.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace PurrNet.Services.Editor.Telemetry
{
    public class PurrTelemetryWindow : EditorWindow
    {
        PurrUserProfile _profile;
        ProjectInfo[] _projects;
        string _error;
        bool _isBusy;
        Vector2 _scrollPos;
        Texture2D _logo;

        static readonly Color HEADER_BG = new(0.17f, 0.17f, 0.17f, 1f);
        static readonly Color SEPARATOR_COLOR = new(0.13f, 0.13f, 0.13f, 1f);
        static readonly Color LINKED_COLOR = new(0.5f, 0.95f, 0.5f, 1f);
        static readonly Color ACCENT_COLOR = new(0.4f, 0.7f, 1f, 1f);
        const string DASHBOARD_URL = "https://purrnet.dev/dashboard";

        const float HEADER_HEIGHT = 42f;

        [NonSerialized] GUIStyle _smallLabelStyle;
        [NonSerialized] GUIStyle _titleStyle;
        [NonSerialized] GUIStyle _bodyStyle;

        [MenuItem("Tools/PurrNet/PurrTelemetry", false, -97)]
        public static void ShowWindow()
        {
            var window = GetWindow<PurrTelemetryWindow>();
            var icon = Resources.Load<Texture2D>("purricon");
            window.titleContent = new GUIContent("PurrTelemetry", icon);
            window.minSize = new Vector2(460, 360);
        }

        void OnEnable()
        {
            _logo = Resources.Load<Texture2D>("purricon");
            _profile = new PurrUserProfile(Repaint);
            _profile.Refresh();
            PurrPackageManagerAuth.onAuthChanged += OnAuthChanged;

            if (PurrPackageManagerAuth.IsLoggedIn)
                RefreshProjects();
        }

        void OnDisable()
        {
            PurrPackageManagerAuth.onAuthChanged -= OnAuthChanged;
        }

        void OnAuthChanged()
        {
            _profile.Refresh();
            _projects = null;
            _error = null;
            if (PurrPackageManagerAuth.IsLoggedIn)
                RefreshProjects();
            Repaint();
        }

        void InitStyles()
        {
            if (_titleStyle != null) return;

            _smallLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f, 1f) }
            };

            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14
            };

            _bodyStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                normal = { textColor = new Color(0.78f, 0.78f, 0.78f, 1f) }
            };
        }

        async void RefreshProjects()
        {
            if (_isBusy) return;
            _isBusy = true;
            _error = null;

            try
            {
                if (!PurrPackageManagerAuth.TryGetApiKey(out var apiKey))
                {
                    _projects = null;
                    return;
                }

                var result = await PurrServicesAPI.GetProjects(apiKey);
                if (result.Success)
                    _projects = result.Value.projects;
                else
                {
                    _error = result.Error;
                    _projects = null;
                }
            }
            catch (Exception e)
            {
                _error = e.Message;
            }
            finally
            {
                _isBusy = false;
                Repaint();
            }
        }

        void OnGUI()
        {
            InitStyles();

            DrawHeader();
            DrawSeparator();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            EditorGUILayout.Space(8);

            DrawProjectSection();

            EditorGUILayout.EndScrollView();
        }

        void DrawHeader()
        {
            var headerRect = GUILayoutUtility.GetRect(0, HEADER_HEIGHT, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(headerRect, HEADER_BG);

            var logoRect = new Rect(headerRect.x + 10, headerRect.y + 7, 28, 28);
            if (_logo != null)
                GUI.DrawTexture(logoRect, _logo, ScaleMode.ScaleToFit);

            var labelRect = new Rect(logoRect.xMax + 8, headerRect.y + 4, 220, 20);
            GUI.Label(labelRect, "PurrTelemetry", new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 });

            if (PurrTelemetrySettings.isLinked)
            {
                var linkedRect = new Rect(labelRect.x, labelRect.yMax - 2, 220, 16);
                GUI.Label(linkedRect, PurrTelemetrySettings.projectName, _smallLabelStyle);
            }

            var refreshRect = new Rect(headerRect.xMax - 78, headerRect.y + 10, 68, 22);
            GUI.enabled = !_isBusy;
            if (GUI.Button(refreshRect, "Refresh"))
                RefreshProjects();
            GUI.enabled = true;

            if (_profile != null)
            {
                var profileAnchor = new Rect(headerRect.x, headerRect.y + 10, refreshRect.x - 4 - headerRect.x, 22);
                _profile.DrawProfileBar(profileAnchor, _smallLabelStyle);
            }
        }

        void DrawSeparator()
        {
            var rect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, SEPARATOR_COLOR);
        }

        void DrawProjectSection()
        {
            EditorGUILayout.LabelField("Project", _titleStyle);
            EditorGUILayout.Space(4);

            DrawLinkedProjectSummary();

            EditorGUILayout.Space(8);

            if (PurrPackageManagerAuth.IsLoggedIn)
                DrawProjectPicker();
            else
                DrawLoginPrompt();

            if (PurrTelemetrySettings.isLinked)
            {
                EditorGUILayout.Space(8);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.color = ACCENT_COLOR;
                    if (GUILayout.Button("Send Test Event", GUILayout.Height(24)))
                        SendTestEvent();
                    GUI.color = Color.white;

                    if (GUILayout.Button("Open Dashboard", GUILayout.Height(24)))
                        Application.OpenURL($"{DASHBOARD_URL}/{PurrTelemetrySettings.projectId}/telemetry");
                }
            }
        }

        void DrawLinkedProjectSummary()
        {
            if (!PurrTelemetrySettings.isLinked)
            {
                EditorGUILayout.LabelField("No project linked.", _bodyStyle);
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    GUI.color = LINKED_COLOR;
                    EditorGUILayout.LabelField(PurrTelemetrySettings.projectName, EditorStyles.boldLabel);
                    GUI.color = Color.white;
                    EditorGUILayout.LabelField($"id: {PurrTelemetrySettings.projectId}", _smallLabelStyle);
                }
            }
        }

        void DrawLoginPrompt()
        {
            EditorGUILayout.HelpBox(
                "Login with Discord to change which project this Unity project sends telemetry to. " +
                "Runtime sends and Send Test Event work for everyone using the committed link.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Login with Discord", GUILayout.Height(22), GUILayout.Width(180)))
                    PurrPackageManagerAuth.Login();
            }
        }

        void DrawProjectPicker()
        {
            if (_error != null)
            {
                EditorGUILayout.HelpBox(_error, MessageType.Error);
                if (GUILayout.Button("Retry", GUILayout.Height(22)))
                    RefreshProjects();
                return;
            }

            if (_isBusy && _projects == null)
            {
                EditorGUILayout.LabelField("Loading projects...", _bodyStyle);
                return;
            }

            if (_projects == null || _projects.Length == 0)
            {
                EditorGUILayout.LabelField("No projects yet. Create one from the PurrServices window.", _bodyStyle);
                if (GUILayout.Button("Open PurrServices", GUILayout.Height(22), GUILayout.Width(160)))
                    PurrServicesSetupWindow.ShowWindow();
                return;
            }

            int currentIndex = 0;
            string[] names = new[] { "—" }.Concat(_projects.Select(p => p.name)).ToArray();
            var linkedId = PurrTelemetrySettings.projectId;
            if (!string.IsNullOrEmpty(linkedId))
            {
                for (int i = 0; i < _projects.Length; i++)
                {
                    if (_projects[i].id == linkedId)
                    {
                        currentIndex = i + 1;
                        break;
                    }
                }
            }

            int picked = EditorGUILayout.Popup("Change Linked Project", currentIndex, names);
            if (picked != currentIndex)
            {
                if (picked == 0)
                    UnlinkProject();
                else
                    LinkProject(_projects[picked - 1]);
            }
        }

        void LinkProject(ProjectInfo project)
        {
            ApplicationConstants.Set(PurrTelemetrySettings.KeyProjectId, project.id);
            ApplicationConstants.Set(PurrTelemetrySettings.KeyPublicKey, project.publicKey);
            ApplicationConstants.Set(PurrTelemetrySettings.KeyProjectName, project.name);
            Repaint();
        }

        void UnlinkProject()
        {
            ApplicationConstants.Delete(PurrTelemetrySettings.KeyProjectId);
            ApplicationConstants.Delete(PurrTelemetrySettings.KeyPublicKey);
            ApplicationConstants.Delete(PurrTelemetrySettings.KeyProjectName);
            Repaint();
        }

        async void SendTestEvent()
        {
            if (!PurrTelemetrySettings.isLinked) return;

            try
            {
                var url = PurrTelemetrySettings.baseUrl.TrimEnd('/') + "/api/services/telemetry/events";
                var body = "{\"event_name\":\"_sdk_test\",\"properties\":{\"sent_from\":\"editor\"},\"source\":\"editor\"}";

                using var req = new UnityWebRequest(url, "POST")
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                    downloadHandler = new DownloadHandlerBuffer()
                };
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {PurrTelemetrySettings.publicKey}");

                await req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                    Debug.Log("[PurrTelemetry] Test event sent.");
                else
                    Debug.LogWarning($"[PurrTelemetry] Test event failed ({req.responseCode}): {req.downloadHandler?.text}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PurrTelemetry] Test event error: {e.Message}");
            }
        }
    }
}
