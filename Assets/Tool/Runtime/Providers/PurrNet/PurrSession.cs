using System;
using UnityEngine;

namespace PurrLobby
{
    [CreateAssetMenu(menuName = "PurrNet/Lobby/Providers/PurrNet/Session Provider")]
    public sealed class PurrSession : SessionProvider
    {
        [SerializeField] private string _apiUrl = "https://purrnet.dev";
        [SerializeField] private string _projectClientKey;

        bool _loggedIn;
        string _id, _name;

        public override bool isLoggedIn => _loggedIn;

        public override string playerId => _id;

        public override string playerName => _name;

        public string apiUrl => _apiUrl;

        public string projectClientKey => _projectClientKey;

        public override void Login(Action<APIResponse> onComplete)
        {
            _id = Guid.NewGuid().ToString("N")[..8];
            _name = SystemInfo.deviceName;
            _loggedIn = true;
            onComplete?.Invoke(APIResponse.Success());

            cookies.ClearCookies();
            cookies.SetCookie("Authorization", _projectClientKey);
            cookies.SetCookie("X-Player-Id", _id);
            cookies.SetCookie("X-Player-Name", _name);
        }

        public void SetPlayerToken(string token)
        {
            cookies.SetCookie("X-Player-Token", token);
        }

        public void ClearPlayerToken()
        {
            cookies.RemoveCookie("X-Player-Token");
        }

        public override void Logout()
        {
            _loggedIn = false;
            _id = null;
            _name = null;
        }
    }
}
