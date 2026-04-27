using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace PurrNet.Services
{
    public class AuthService
    {
        const string PREFS_SESSION_TOKEN = "PurrServices_SessionToken";

        static string PrefsKey
        {
            get
            {
#if UNITY_EDITOR
                var projectPath = Application.dataPath;
                var folderName = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(projectPath));
                return PREFS_SESSION_TOKEN + "_" + folderName;
#else
                return PREFS_SESSION_TOKEN;
#endif
            }
        }

        readonly ServiceHttp _http;

        string _sessionToken;
        string _playerId;
        string _displayName;
        string _expiresAt;

        public bool isAuthenticated => !string.IsNullOrEmpty(_sessionToken);
        public string sessionToken => _sessionToken;
        public string playerId => _playerId;
        public string displayName => _displayName;

        public event Action onLoggedIn;
        public event Action onLoggedOut;

        internal AuthService(ServiceHttp http)
        {
            _http = http;
            _sessionToken = PlayerPrefs.GetString(PrefsKey, null);
        }

        public async Task<AuthResult> LoginAsync(string deviceId, string displayName = null)
        {
            var credentials = new Dictionary<string, string> { { "deviceId", deviceId } };

            if (!string.IsNullOrEmpty(displayName))
                credentials["displayName"] = displayName;

            return await LoginAsync("system-uuid", credentials);
        }

        public async Task<AuthResult> RegisterAsync(string username, string password, string displayName = null)
        {
            var credentials = new Dictionary<string, string>
            {
                { "username", username },
                { "password", password },
                { "mode", "register" }
            };

            if (!string.IsNullOrEmpty(displayName))
                credentials["displayName"] = displayName;

            return await LoginAsync("username-password", credentials);
        }

        public async Task<AuthResult> LoginWithPasswordAsync(string username, string password)
        {
            var credentials = new Dictionary<string, string>
            {
                { "username", username },
                { "password", password },
                { "mode", "login" }
            };

            return await LoginAsync("username-password", credentials);
        }

        public async Task<AuthResult> LoginAsync(string provider, Dictionary<string, string> credentials)
        {
            var request = new AuthRequest
            {
                provider = provider,
                credentials = credentials
            };

            var response = await _http.PostAsync<AuthResponse>("/api/lobby/auth", request);

            if (!response.success)
            {
                return new AuthResult
                {
                    success = false,
                    error = response.error
                };
            }

            _sessionToken = response.data.sessionToken;
            _playerId = response.data.playerId;
            _displayName = response.data.displayName;
            _expiresAt = response.data.expiresAt;

            SaveSession();
            onLoggedIn?.Invoke();

            return new AuthResult
            {
                success = true,
                playerId = _playerId,
                displayName = _displayName
            };
        }

        /// <summary>
        /// Validates a previously saved session token against the server.
        /// On success, restores playerId and displayName and fires onLoggedIn.
        /// On failure, clears the saved session.
        /// </summary>
        public async Task<AuthResult> ValidateSessionAsync()
        {
            if (string.IsNullOrEmpty(_sessionToken))
            {
                return new AuthResult
                {
                    success = false,
                    error = "No saved session token"
                };
            }

            var response = await _http.GetAsync<SessionValidationResponse>("/api/lobby/auth");

            if (!response.success)
            {
                ClearSession();
                return new AuthResult
                {
                    success = false,
                    error = response.error
                };
            }

            _playerId = response.data.playerId;
            _displayName = response.data.displayName;
            _expiresAt = response.data.expiresAt;

            onLoggedIn?.Invoke();

            return new AuthResult
            {
                success = true,
                playerId = _playerId,
                displayName = _displayName
            };
        }

        public void Logout()
        {
            // Disconnect all lobby connections before clearing the session
            PurrServices.instance.DisconnectAllConnections();
            ClearSession();
            onLoggedOut?.Invoke();
        }

        void SaveSession()
        {
            PlayerPrefs.SetString(PrefsKey, _sessionToken);
            PlayerPrefs.Save();
        }

        void ClearSession()
        {
            _sessionToken = null;
            _playerId = null;
            _displayName = null;
            _expiresAt = null;

            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }
    }
}
