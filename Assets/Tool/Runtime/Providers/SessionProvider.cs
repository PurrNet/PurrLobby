using System;
using System.Threading.Tasks;
using PurrNet.UI;
using UnityEngine;

namespace PurrLobby
{
    public abstract class SessionProvider : ScriptableObject
    {
        public abstract bool isLoggedIn { get; }

        public abstract string playerId { get; }

        public abstract string playerName { get; }

        [NonSerialized]
        public readonly SessionCookies cookies = new SessionCookies();

        public abstract Task Login(ViewStack stack);

        public abstract Task Logout();
    }
}
