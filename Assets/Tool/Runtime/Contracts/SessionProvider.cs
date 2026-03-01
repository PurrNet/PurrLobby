using System;
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

        public abstract void Login(Action<APIResponse> onComplete);

        public virtual void Logout() {}
    }
}
