using System;
using PurrNet.Editor;
using UnityEditor;
using UnityEngine;

namespace PurrNet.Services.Editor
{
    public class PurrServicesSetupWindow : EditorWindow
    {
        static string LinkedProjectIdKey => "PurrServices_LinkedProjectId_" + Application.dataPath;
        static string LinkedProjectNameKey => "PurrServices_LinkedProjectName_" + Application.dataPath;

        PurrUserProfile _profile;
        ProjectInfo[] _projects;
        string _error;
        bool _isBusy;
        Vector2 _listScrollPos;
        Vector2 _detailScrollPos;
        string _newProjectName = "";
        bool _showCreateField;
        Texture2D _logo;

        int _selectedIndex = -1;

        string _linkedProjectId;
        string _linkedProjectName;

        static readonly Color HEADER_BG = new(0.17f, 0.17f, 0.17f, 1f);
        static readonly Color SEPARATOR_COLOR = new(0.13f, 0.13f, 0.13f, 1f);
        static readonly Color LIST_BG = new(0.2f, 0.2f, 0.2f, 1f);
        static readonly Color SELECTED_BG = new(0.17f, 0.36f, 0.53f, 1f);
        static readonly Color HOVER_BG = new(0.26f, 0.26f, 0.26f, 1f);
        static readonly Color SELECTED_ACCENT = new(0.35f, 0.65f, 0.95f, 1f);
        static readonly Color LINKED_COLOR = new(0.5f, 0.95f, 0.5f, 1f);
        static readonly Color ACCENT_COLOR = new(0.4f, 0.7f, 1f, 1f);
        const string DASHBOARD_URL = "https://purrnet.dev/dashboard";

        const float HEADER_HEIGHT = 42f;
        const float ITEM_HEIGHT = 28f;
        const float SPLITTER_WIDTH = 6f;
        const float SPLIT_MARGIN = 80f;

        float _splitWidth = 220f;
        bool _isDraggingSplitter;
        Rect _cachedSplitterRect;

        [NonSerialized] GUIStyle _smallLabelStyle;
        [NonSerialized] GUIStyle _itemNameStyle;
        [NonSerialized] GUIStyle _itemDetailStyle;
        [NonSerialized] GUIStyle _detailTitleStyle;
        [NonSerialized] GUIStyle _detailDescStyle;

        [MenuItem("Tools/PurrNet/PurrServices", false, -98)]
        public static void ShowWindow()
        {
            var window = GetWindow<PurrServicesSetupWindow>();
            var icon = Resources.Load<Texture2D>("purricon");
            window.titleContent = new GUIContent("PurrServices", icon);
            window.minSize = new Vector2(520, 350);
        }

        void InitStyles()
        {
            if (_detailTitleStyle != null) return;

            _smallLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f, 1f) }
            };

            _itemNameStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                padding = new RectOffset(12, 4, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                alignment = TextAnchor.MiddleLeft
            };

            _itemDetailStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                padding = new RectOffset(0, 6, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f, 1f) }
            };

            _detailTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                margin = new RectOffset(0, 0, 0, 4)
            };

            _detailDescStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                fontSize = 11,
                normal = { textColor = new Color(0.78f, 0.78f, 0.78f, 1f) }
            };

        }

        void OnEnable()
        {
            wantsMouseMove = true;
            _logo = Resources.Load<Texture2D>("purricon");
            _profile = new PurrUserProfile(Repaint);
            _profile.Refresh();
            PurrPackageManagerAuth.onAuthChanged += OnAuthChanged;

            _linkedProjectId = EditorPrefs.GetString(LinkedProjectIdKey, "");
            _linkedProjectName = EditorPrefs.GetString(LinkedProjectNameKey, "");

            if (PurrPackageManagerAuth.HasApiKey())
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
            _selectedIndex = -1;
            if (PurrPackageManagerAuth.HasApiKey())
                RefreshProjects();
            Repaint();
        }

        async void RefreshProjects()
        {
            if (_isBusy) return;
            _isBusy = true;
            _error = null;

            try
            {
                var result = await PurrServicesAPI.GetProjects(PurrPackageManagerAuth.GetApiKey());
                if (result.Success)
                {
                    _projects = result.Value.projects;
                    if (_selectedIndex >= _projects.Length)
                        _selectedIndex = _projects.Length - 1;
                    if (_selectedIndex < 0 && !string.IsNullOrEmpty(_linkedProjectId))
                    {
                        for (int i = 0; i < _projects.Length; i++)
                        {
                            if (_projects[i].id == _linkedProjectId)
                            {
                                _selectedIndex = i;
                                break;
                            }
                        }
                    }
                }
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

        async void CreateProject(string projectName)
        {
            if (_isBusy || string.IsNullOrWhiteSpace(projectName)) return;
            _isBusy = true;
            _error = null;

            try
            {
                var result = await PurrServicesAPI.CreateProject(PurrPackageManagerAuth.GetApiKey(), projectName);
                if (result.Success)
                {
                    _newProjectName = "";
                    _showCreateField = false;
                    await RefreshProjectsAsync();
                    if (_projects != null)
                    {
                        for (int i = 0; i < _projects.Length; i++)
                        {
                            if (_projects[i].id == result.Value.project.id)
                            {
                                SelectProject(i);
                                break;
                            }
                        }
                    }
                }
                else
                    _error = result.Error;
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

        async System.Threading.Tasks.Task RefreshProjectsAsync()
        {
            var result = await PurrServicesAPI.GetProjects(PurrPackageManagerAuth.GetApiKey());
            if (result.Success)
                _projects = result.Value.projects;
        }

        void SelectProject(int index)
        {
            if (_selectedIndex == index) return;
            _selectedIndex = index;
            _detailScrollPos = Vector2.zero;
            Repaint();
        }

        void LinkProject(ProjectInfo project)
        {
            _linkedProjectId = project.id;
            _linkedProjectName = project.name;
            EditorPrefs.SetString(LinkedProjectIdKey, project.id);
            EditorPrefs.SetString(LinkedProjectNameKey, project.name);
        }

        void UnlinkProject()
        {
            _linkedProjectId = "";
            _linkedProjectName = "";
            EditorPrefs.DeleteKey(LinkedProjectIdKey);
            EditorPrefs.DeleteKey(LinkedProjectNameKey);
        }

        void OnGUI()
        {
            InitStyles();
            HandleSplitterDrag(_cachedSplitterRect);

            DrawHeader();
            DrawSeparator();

            if (!PurrPackageManagerAuth.HasApiKey())
            {
                DrawCenteredMessage("Sign in to manage your projects.");
                GUILayout.Space(4);
                DrawCenteredButton("Login with Discord", PurrPackageManagerAuth.Login);
                return;
            }

            if (_isBusy && _projects == null)
            {
                DrawCenteredMessage("Loading projects...");
                return;
            }

            if (_error != null)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(_error, MessageType.Error);
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Retry", GUILayout.Height(24)))
                    RefreshProjects();
                return;
            }

            if (_projects == null || _projects.Length == 0)
            {
                DrawCenteredMessage("No projects yet. Create one below.");
                GUILayout.Space(4);
                DrawCreateProject();
                return;
            }

            if (_selectedIndex >= _projects.Length)
                _selectedIndex = _projects.Length - 1;
            if (_selectedIndex < 0 && _projects.Length > 0)
                SelectProject(0);

            _splitWidth = Mathf.Clamp(_splitWidth, SPLIT_MARGIN, position.width - SPLIT_MARGIN);

            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

            EditorGUILayout.BeginVertical(GUILayout.Width(_splitWidth));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            var listRect = GUILayoutUtility.GetLastRect();

            GUILayout.Space(SPLITTER_WIDTH);

            DrawDetail();

            EditorGUILayout.EndHorizontal();

            if (Event.current.type != EventType.Layout)
            {
                var fullListRect = new Rect(0, listRect.y, _splitWidth, listRect.height);
                _cachedSplitterRect = new Rect(_splitWidth, listRect.y, SPLITTER_WIDTH, listRect.height);
                DrawProjectList(fullListRect);
                DrawSplitter(_cachedSplitterRect);
            }

            DrawSeparator();
            DrawCreateProject();
            GUILayout.Space(4);
        }

        void DrawHeader()
        {
            var headerRect = GUILayoutUtility.GetRect(0, HEADER_HEIGHT, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(headerRect, HEADER_BG);

            var logoRect = new Rect(headerRect.x + 10, headerRect.y + 7, 28, 28);
            if (_logo != null)
                GUI.DrawTexture(logoRect, _logo, ScaleMode.ScaleToFit);

            var labelRect = new Rect(logoRect.xMax + 8, headerRect.y + 4, 200, 20);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            GUI.Label(labelRect, "PurrServices", headerStyle);

            if (!string.IsNullOrEmpty(_linkedProjectName))
            {
                var linkedRect = new Rect(labelRect.x, labelRect.yMax - 2, 200, 16);
                GUI.Label(linkedRect, _linkedProjectName, _smallLabelStyle);
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

        void DrawSplitter(Rect rect)
        {
            EditorGUI.DrawRect(rect, SEPARATOR_COLOR);

            float centerX = rect.x + rect.width / 2f;
            float centerY = rect.y + rect.height / 2f;
            var dotColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            for (int i = -2; i <= 2; i++)
            {
                var dotRect = new Rect(centerX - 1, centerY + i * 5, 2, 2);
                EditorGUI.DrawRect(dotRect, dotColor);
            }

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
        }

        void HandleSplitterDrag(Rect splitterRect)
        {
            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown when splitterRect.Contains(e.mousePosition):
                    _isDraggingSplitter = true;
                    e.Use();
                    break;
                case EventType.MouseDrag when _isDraggingSplitter:
                    _splitWidth = Mathf.Clamp(e.mousePosition.x, SPLIT_MARGIN, position.width - SPLIT_MARGIN);
                    e.Use();
                    Repaint();
                    break;
                case EventType.MouseUp when _isDraggingSplitter:
                    _isDraggingSplitter = false;
                    e.Use();
                    break;
            }
        }

        void DrawProjectList(Rect areaRect)
        {
            EditorGUI.DrawRect(areaRect, LIST_BG);

            float totalHeight = _projects.Length * ITEM_HEIGHT;
            bool needsScroll = totalHeight > areaRect.height;
            var viewRect = new Rect(0, 0, areaRect.width - (needsScroll ? 13f : 0f), totalHeight);

            _listScrollPos = GUI.BeginScrollView(areaRect, _listScrollPos, viewRect);

            for (int i = 0; i < _projects.Length; i++)
            {
                var project = _projects[i];
                var itemRect = new Rect(0, i * ITEM_HEIGHT, viewRect.width, ITEM_HEIGHT);
                bool isSelected = i == _selectedIndex;
                bool isLinked = project.id == _linkedProjectId;
                bool isHover = itemRect.Contains(Event.current.mousePosition) && !isSelected;

                if (isSelected)
                {
                    EditorGUI.DrawRect(itemRect, SELECTED_BG);
                    EditorGUI.DrawRect(new Rect(itemRect.x, itemRect.y, 3, itemRect.height),
                        isLinked ? LINKED_COLOR : SELECTED_ACCENT);
                }
                else if (isHover)
                {
                    EditorGUI.DrawRect(itemRect, HOVER_BG);
                }

                if (isLinked)
                {
                    var dotRect = new Rect(itemRect.xMax - 12, itemRect.y + (itemRect.height - 6) / 2, 6, 6);
                    EditorGUI.DrawRect(dotRect, LINKED_COLOR);
                }

                if (Event.current.type == EventType.Repaint)
                    _itemNameStyle.Draw(itemRect, project.name, false, false, false, false);

                if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
                {
                    SelectProject(i);
                    GUI.FocusControl(null);
                    Event.current.Use();
                }

                if (isHover && Event.current.type == EventType.Repaint)
                    Repaint();
            }

            GUI.EndScrollView();
        }

        void DrawDetail()
        {
            if (_projects == null || _selectedIndex < 0 || _selectedIndex >= _projects.Length)
            {
                EditorGUILayout.BeginVertical();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Select a project", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            var project = _projects[_selectedIndex];
            bool isLinked = project.id == _linkedProjectId;

            _detailScrollPos = EditorGUILayout.BeginScrollView(_detailScrollPos);
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField(project.name, _detailTitleStyle);
            EditorGUILayout.LabelField(project.slug, _smallLabelStyle);
            EditorGUILayout.Space(8);

            DrawInfoRow("Created", FormatDate(project.createdAt));
            if (!string.IsNullOrEmpty(project.publicKey))
            {
                var truncated = project.publicKey.Length > 28
                    ? project.publicKey[..28] + "..."
                    : project.publicKey;
                DrawInfoRow("Public Key", truncated);
            }

            EditorGUILayout.Space(12);

            if (isLinked)
            {
                GUI.color = LINKED_COLOR;
                EditorGUILayout.LabelField("This project is linked to this Unity project.", _detailDescStyle);
                GUI.color = Color.white;
                EditorGUILayout.Space(4);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Unlink", GUILayout.Height(24), GUILayout.Width(80)))
                    UnlinkProject();

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField("Link this project to configure PurrServices in your scenes.", _detailDescStyle);
                EditorGUILayout.Space(4);
                GUI.color = ACCENT_COLOR;
                if (GUILayout.Button("Link to this project", GUILayout.Height(26), GUILayout.Width(160)))
                    LinkProject(project);
                GUI.color = Color.white;
            }

            EditorGUILayout.Space(16);

            if (GUILayout.Button("Open Dashboard", GUILayout.Height(24), GUILayout.Width(130)))
                Application.OpenURL($"{DASHBOARD_URL}/{project.id}");

            EditorGUILayout.EndScrollView();
        }

        void DrawInfoRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _smallLabelStyle, GUILayout.Width(80));
            GUILayout.Label(value, _itemNameStyle);
            GUILayout.EndHorizontal();
        }

        static string FormatDate(string isoDate)
        {
            if (DateTime.TryParse(isoDate, out var dt))
                return dt.ToString("yyyy-MM-dd");
            return isoDate ?? "—";
        }

        void DrawCreateProject()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(4);

            if (!_showCreateField)
            {
                if (GUILayout.Button("+ New Project", GUILayout.Height(22)))
                    _showCreateField = true;
            }
            else
            {
                _newProjectName = EditorGUILayout.TextField(_newProjectName, GUILayout.Height(20));

                GUI.enabled = !_isBusy && !string.IsNullOrWhiteSpace(_newProjectName);
                if (GUILayout.Button("Create", GUILayout.Width(55), GUILayout.Height(20)))
                    CreateProject(_newProjectName);
                GUI.enabled = true;

                if (GUILayout.Button("Cancel", GUILayout.Width(50), GUILayout.Height(20)))
                {
                    _showCreateField = false;
                    _newProjectName = "";
                }
            }

            GUILayout.Space(4);
            EditorGUILayout.EndHorizontal();
        }

        void DrawCenteredMessage(string text)
        {
            EditorGUILayout.Space(40);
            var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 12 };
            GUILayout.Label(text, style);
        }

        void DrawCenteredButton(string text, Action action)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(text, GUILayout.Height(28), GUILayout.Width(180)))
                action?.Invoke();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }
}
