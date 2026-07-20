using System.Threading.Tasks;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public abstract class SessionProvider : ScriptableObject
    {
        public abstract bool isLoggedIn { get; }

        public abstract string playerId { get; }

        public abstract string playerName { get; }

        public abstract Task Login(ViewStack stack);

        public abstract Task Logout();
    }
}
