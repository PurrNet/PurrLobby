using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace PurrNet.Lobby.Edgegap
{
    /// <summary>
    /// Inspector for <see cref="EdgegapMatchmakingProvider"/>. Adds field grouping,
    /// inline validation, a "Test Matchmaker" button, and a browser/CORS preflight
    /// check so problems surface before play mode.
    /// Deliberately depends only on Unity - not on the Edgegap Unity plugin.
    /// </summary>
    [CustomEditor(typeof(EdgegapMatchmakingProvider))]
    public class EdgegapMatchmakingProviderEditor : UnityEditor.Editor
    {
        private static readonly HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private const string DASHBOARD_URL = "https://app.edgegap.com/";
        private const string DOCS_URL = "https://docs.edgegap.com/learn/matchmaking";
        private const string SHOW_HINTS_KEY = "PurrLobby.Edgegap.ShowHints";

        private const double FETCH_DEBOUNCE_SECONDS = 0.8;
        private const string PROBE_ORIGIN = "https://purrlobby-cors-check.example.com";

        private SerializedProperty _matchmakerUrl;
        private SerializedProperty _authToken;
        private SerializedProperty _defaultProfile;
        private SerializedProperty _gameAllocator;
        private SerializedProperty _pollIntervalMs;
        private SerializedProperty _timeoutMs;

        private bool _showHints;

        private bool _testing;
        private bool _hasTestResult;
        private bool _testOk;
        private string _testMessage;

        private bool _corsTesting;
        private bool _hasCorsResult;
        private MessageType _corsResultType;
        private string _corsMessage;

        private string[] _profiles;

        private string _autoFetchedUrl;
        private string _autoFetchedToken;
        private string _pendingFetchUrl;
        private string _pendingFetchToken;
        private double _pendingFetchAt;
        private bool _autoFetching;

        private string _allocatorMismatch;

        private void OnEnable()
        {
            _matchmakerUrl = serializedObject.FindProperty("_matchmakerUrl");
            _authToken = serializedObject.FindProperty("_authToken");
            _defaultProfile = serializedObject.FindProperty("_defaultProfile");
            _gameAllocator = serializedObject.FindProperty("_gameAllocator");
            _pollIntervalMs = serializedObject.FindProperty("_pollIntervalMs");
            _timeoutMs = serializedObject.FindProperty("_timeoutMs");

            _showHints = EditorPrefs.GetBool(SHOW_HINTS_KEY, true);

            EditorApplication.update += OnEditorUpdate;
            ScheduleProfileFetch(debounced: false);
            RefreshAllocatorPairing();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Matchmaker", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_matchmakerUrl);
            EditorGUILayout.PropertyField(_authToken);
            var credsEdited = EditorGUI.EndChangeCheck();
            DrawProfileField();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Connection", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_gameAllocator);
            var allocatorEdited = EditorGUI.EndChangeCheck();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Polling", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_pollIntervalMs);
            EditorGUILayout.PropertyField(_timeoutMs);

            serializedObject.ApplyModifiedProperties();

            if (credsEdited)
                ScheduleProfileFetch(debounced: true);

            if (allocatorEdited)
                RefreshAllocatorPairing();

            DrawHints();
            DrawTestSection();
            DrawLinks();
        }

        /// <summary>
        /// Queues a background fetch of the matchmaker's profile list for the current
        /// URL/token. Debounced so typing doesn't spam requests; a no-op when the
        /// creds are unchanged or the URL isn't a usable http(s) address.
        /// </summary>
        private void ScheduleProfileFetch(bool debounced)
        {
            var url = (_matchmakerUrl.stringValue ?? string.Empty).Trim();
            var token = _authToken.stringValue ?? string.Empty;

            if (!IsHttpUrl(url))
            {
                _pendingFetchUrl = null;
                _profiles = null;
                return;
            }

            if (url == _autoFetchedUrl && token == _autoFetchedToken)
                return;

            if (url != _autoFetchedUrl)
                _profiles = null;

            _pendingFetchUrl = url;
            _pendingFetchToken = token;
            _pendingFetchAt = EditorApplication.timeSinceStartup + (debounced ? FETCH_DEBOUNCE_SECONDS : 0);
        }

        private void OnEditorUpdate()
        {
            if (_pendingFetchUrl == null || _autoFetching)
                return;
            if (EditorApplication.timeSinceStartup < _pendingFetchAt)
                return;

            var url = _pendingFetchUrl;
            var token = _pendingFetchToken;
            _pendingFetchUrl = null;
            _ = RefreshProfiles(url, token);
        }

        /// <summary>
        /// Draws the profile field. Once the matchmaker's profile list has been
        /// recovered from its OpenAPI document, this becomes a dropdown; otherwise
        /// it stays a free text field.
        /// </summary>
        private void DrawProfileField()
        {
            if (_profiles == null || _profiles.Length == 0)
            {
                EditorGUILayout.PropertyField(_defaultProfile);
                return;
            }

            var options = _profiles.ToList();
            var current = _defaultProfile.stringValue;

            if (!string.IsNullOrEmpty(current) && !options.Contains(current))
                options.Insert(0, current);

            var index = Mathf.Max(0, options.IndexOf(current));
            var label = new GUIContent(_defaultProfile.displayName, _defaultProfile.tooltip);
            var newIndex = EditorGUILayout.Popup(label, index, options.Select(o => new GUIContent(o)).ToArray());

            if (newIndex != index)
                _defaultProfile.stringValue = options[newIndex];
        }

        /// <summary>
        /// All the automatic validation and build-target notes, behind a foldout so
        /// they can be collapsed once they've been read.
        /// </summary>
        private void DrawHints()
        {
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            _showHints = EditorGUILayout.Foldout(_showHints, "Inspector Hints", true);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetBool(SHOW_HINTS_KEY, _showHints);

            if (!_showHints)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                DrawValidation();
                DrawBuildTargetNotes();

                if (_allocatorMismatch != null)
                    EditorGUILayout.HelpBox(_allocatorMismatch, MessageType.Error);
            }
        }

        /// <summary>
        /// The matchmaker reads its transport and port from the Game Allocator it
        /// references, so that allocator should be the same one the orchestrator
        /// pairs this matchmaker with. This scans the orchestrators and flags any
        /// disagreement. Done by name/SerializedProperty so it needs no extra
        /// assembly reference.
        /// </summary>
        private void RefreshAllocatorPairing()
        {
            _allocatorMismatch = null;

            try
            {
                var referenced = _gameAllocator.objectReferenceValue;

                foreach (var guid in AssetDatabase.FindAssets("t:GameOrchestrator"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var orchestrator = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (orchestrator == null)
                        continue;

                    var orchestratorSo = new SerializedObject(orchestrator);
                    if (orchestratorSo.FindProperty("matchmakingProvider")?.objectReferenceValue != target)
                        continue;

                    var paired = orchestratorSo.FindProperty("gameAllocator")?.objectReferenceValue;
                    if (paired == referenced)
                        continue;

                    _allocatorMismatch =
                        $"Orchestrator '{orchestrator.name}' pairs this matchmaker with allocator " +
                        $"'{(paired != null ? paired.name : "none")}', but this matchmaker references " +
                        $"'{(referenced != null ? referenced.name : "none")}'. The matchmaker reads its " +
                        "transport and port from the allocator it references - point both at the same asset.";
                    return;
                }
            }
            catch
            {
                _allocatorMismatch = null;
            }
        }

        private void DrawValidation()
        {
            var url = _matchmakerUrl.stringValue;
            if (string.IsNullOrWhiteSpace(url))
            {
                EditorGUILayout.HelpBox(
                    "Matchmaker URL is required. Copy the API URL from your matchmaker's page in the Edgegap dashboard.",
                    MessageType.Warning);
            }
            else if (!IsHttpUrl(url))
            {
                EditorGUILayout.HelpBox("Matchmaker URL doesn't look like a valid http(s) URL.", MessageType.Warning);
            }

            if (_gameAllocator.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "No Edgegap Game Allocator assigned. The matchmaker reads its transport and " +
                    "connection port from the allocator, so matchmaking fails without one.",
                    MessageType.Warning);
            }

            if (string.IsNullOrWhiteSpace(_authToken.stringValue))
            {
                EditorGUILayout.HelpBox(
                    "No Auth Token set. If your matchmaker requires one, ticket requests will be rejected.",
                    MessageType.Info);
            }

            if (_pollIntervalMs.intValue < 1000)
            {
                EditorGUILayout.HelpBox(
                    "Poll interval is under 1s. Edgegap recommends polling every 3-5 seconds.",
                    MessageType.Warning);
            }

            if (_timeoutMs.intValue <= _pollIntervalMs.intValue)
            {
                EditorGUILayout.HelpBox(
                    "Timeout is shorter than the poll interval - the ticket would time out before its first poll.",
                    MessageType.Warning);
            }
        }

        /// <summary>
        /// Surfaces problems that only depend on the active build target: a WebGL
        /// build talks to the matchmaker from the browser, so it needs CORS, and it
        /// cannot use a raw UDP transport.
        /// </summary>
        private void DrawBuildTargetNotes()
        {
            var allocator = _gameAllocator.objectReferenceValue as EdgegapGameAllocator;
            var isWebGl = EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL;

            if (isWebGl)
            {
                EditorGUILayout.HelpBox(
                    "Active build target is WebGL. The matchmaker is called from the browser, so set " +
                    "`allowed_cors_origins` on the Edgegap matchmaker to the origin your game is served " +
                    "from, or browser clients will be blocked. Use \"Check Browser (CORS) Access\" below.",
                    MessageType.Info);

                if (allocator != null && allocator.Transport == EdgegapAllocatorTransport.UDP)
                {
                    EditorGUILayout.HelpBox(
                        "The paired allocator's transport is UDP but the build target is WebGL. Browsers " +
                        "can't open raw UDP sockets - switch that allocator to a WebSocket transport for " +
                        "WebGL builds.",
                        MessageType.Error);
                }
            }
            else if (allocator != null && allocator.Transport == EdgegapAllocatorTransport.Web)
            {
                EditorGUILayout.HelpBox(
                    "Transport is Web (WebSocket) but the active build target isn't WebGL. That's valid, " +
                    "just make sure it's intentional and matches your Edgegap Game Allocator.",
                    MessageType.Info);
            }
        }

        private void DrawTestSection()
        {
            EditorGUILayout.Space();

            var noUrl = string.IsNullOrWhiteSpace(_matchmakerUrl.stringValue);
            var busy = _testing || _corsTesting;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(busy || noUrl))
                {
                    if (GUILayout.Button(_testing ? "Testing..." : "Test Matchmaker"))
                        _ = TestConnection(_matchmakerUrl.stringValue, _authToken.stringValue);

                    if (GUILayout.Button(_corsTesting ? "Checking..." : "Check Browser (CORS) Access"))
                        _ = TestCors(_matchmakerUrl.stringValue);
                }
            }

            if (_hasTestResult)
                EditorGUILayout.HelpBox(_testMessage, _testOk ? MessageType.Info : MessageType.Error);

            if (_hasCorsResult)
                EditorGUILayout.HelpBox(_corsMessage, _corsResultType);
        }

        private void DrawLinks()
        {
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Edgegap Dashboard"))
                    Application.OpenURL(DASHBOARD_URL);
                if (GUILayout.Button("Matchmaking Docs"))
                    Application.OpenURL(DOCS_URL);
            }
        }

        /// <summary>
        /// Fetches the matchmaker's OpenAPI document. A 200 confirms the URL is
        /// reachable and the token is accepted; 401/403 means the token is wrong.
        /// Also recovers the profile list if the document encodes it.
        /// </summary>
        private async Task TestConnection(string url, string token)
        {
            _testing = true;
            _hasTestResult = false;
            Repaint();

            var swaggerUrl = $"{url.Trim().TrimEnd('/')}/swagger/v1/swagger.json";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, swaggerUrl);

                if (!string.IsNullOrEmpty(token))
                    request.Headers.TryAddWithoutValidation("Authorization", token);

                using var response = await client.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                    response.StatusCode == HttpStatusCode.Forbidden)
                {
                    SetResult(false, $"Reached the matchmaker, but the Auth Token was rejected (HTTP {(int)response.StatusCode}).");
                }
                else if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    var api = TryReadApiInfo(body);
                    _profiles = ExtractProfiles(body);
                    _autoFetchedUrl = url.Trim();
                    _autoFetchedToken = token ?? string.Empty;

                    var message = api != null
                        ? $"Connected. Matchmaker API: {api}."
                        : "Connected. The matchmaker URL is reachable.";
                    if (_profiles != null && _profiles.Length > 0)
                        message += $" Found {_profiles.Length} profile(s).";

                    SetResult(true, message);
                }
                else
                {
                    SetResult(false, $"Reached {url}, but it returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Double-check the URL.");
                }
            }
            catch (Exception e)
            {
                SetResult(false, $"Could not reach the matchmaker: {e.Message}");
            }
            finally
            {
                _testing = false;
                if (this != null)
                    Repaint();
            }
        }

        /// <summary>
        /// Silent background fetch behind the profile dropdown. Unlike "Test
        /// Matchmaker" it never writes a result message - failures just leave the
        /// field as free text, and the Test button stays the place to see real errors.
        /// </summary>
        private async Task RefreshProfiles(string url, string token)
        {
            _autoFetching = true;

            try
            {
                var swaggerUrl = $"{url.TrimEnd('/')}/swagger/v1/swagger.json";

                using var request = new HttpRequestMessage(HttpMethod.Get, swaggerUrl);

                if (!string.IsNullOrEmpty(token))
                    request.Headers.TryAddWithoutValidation("Authorization", token);

                using var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _profiles = ExtractProfiles(body);
                }

                _autoFetchedUrl = url;
                _autoFetchedToken = token;
            }
            catch { /* ignored */ }
            finally
            {
                _autoFetching = false;
                if (this != null)
                    Repaint();
            }
        }

        /// <summary>
        /// Replays the CORS preflight a browser sends before a POST /tickets. If the
        /// matchmaker answers with Access-Control-Allow-Origin, browser/WebGL clients
        /// are allowed; if not, they'd be blocked until `allowed_cors_origins` is set.
        /// (The editor itself isn't CORS-restricted - this probe just mimics a browser.)
        /// </summary>
        private async Task TestCors(string url)
        {
            _corsTesting = true;
            _hasCorsResult = false;
            Repaint();

            var ticketsUrl = $"{url.Trim().TrimEnd('/')}/tickets";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Options, ticketsUrl);
                request.Headers.TryAddWithoutValidation("Origin", PROBE_ORIGIN);
                request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");
                request.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "authorization,content-type");

                using var response = await client.SendAsync(request);

                var allowOrigin = FirstHeader(response, "Access-Control-Allow-Origin");

                if (string.IsNullOrEmpty(allowOrigin))
                {
                    SetCorsResult(MessageType.Warning,
                        "No 'Access-Control-Allow-Origin' header came back. Native builds are unaffected, " +
                        "but a WebGL/browser client would be blocked. Set `allowed_cors_origins` on the " +
                        "Edgegap matchmaker to the origin your game is served from.\n\n" +
                        "(If you whitelisted a specific domain rather than '*', this probe can't match it - " +
                        "the warning may be a false alarm.)");
                }
                else
                {
                    var methods = FirstHeader(response, "Access-Control-Allow-Methods");
                    var detail = string.IsNullOrEmpty(methods) ? "" : $" Allowed methods: {methods}.";
                    SetCorsResult(MessageType.Info,
                        $"CORS is configured (Access-Control-Allow-Origin: {allowOrigin}).{detail} " +
                        "Browser/WebGL clients from a permitted origin can reach the matchmaker.");
                }
            }
            catch (Exception e)
            {
                SetCorsResult(MessageType.Error, $"Could not run the CORS check: {e.Message}");
            }
            finally
            {
                _corsTesting = false;
                if (this != null)
                    Repaint();
            }
        }

        private static bool IsHttpUrl(string url)
        {
            return !string.IsNullOrWhiteSpace(url)
                && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static string FirstHeader(HttpResponseMessage response, string name)
        {
            if (response.Headers.TryGetValues(name, out var values))
                return values.FirstOrDefault();
            if (response.Content != null && response.Content.Headers.TryGetValues(name, out var contentValues))
                return contentValues.FirstOrDefault();
            return null;
        }

        private void SetResult(bool ok, string message)
        {
            _testOk = ok;
            _testMessage = message;
            _hasTestResult = true;
        }

        private void SetCorsResult(MessageType type, string message)
        {
            _corsResultType = type;
            _corsMessage = message;
            _hasCorsResult = true;
        }

        private static string TryReadApiInfo(string swaggerJson)
        {
            try
            {
                var info = JObject.Parse(swaggerJson)["info"];
                if (info == null)
                    return null;

                var title = (string)info["title"];
                var version = (string)info["version"];
                var text = $"{title} {version}".Trim();
                return string.IsNullOrEmpty(text) ? null : text;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Best-effort recovery of the matchmaker's profile names. The matchmaker's
        /// OpenAPI document is generated from its config; if that config's profiles
        /// surface as an enum on a "profile" property, we can read them here. Returns
        /// null when the document doesn't expose them - the field stays free text.
        /// </summary>
        private static string[] ExtractProfiles(string swaggerJson)
        {
            try
            {
                var root = JObject.Parse(swaggerJson);

                foreach (var property in root.Descendants().OfType<JProperty>())
                {
                    if (!string.Equals(property.Name, "profile", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (property.Value is JObject schema && schema["enum"] is JArray values && values.Count > 0)
                    {
                        var profiles = values
                            .Select(v => (string)v)
                            .Where(s => !string.IsNullOrEmpty(s))
                            .Distinct()
                            .ToArray();

                        if (profiles.Length > 0)
                            return profiles;
                    }
                }
            }
            catch { /* ignored */ }

            return null;
        }
    }
}
