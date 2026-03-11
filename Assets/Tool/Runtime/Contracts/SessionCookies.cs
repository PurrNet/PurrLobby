using System.Collections.Generic;

namespace PurrNet.Lobby
{
    public sealed class SessionCookies
    {
        public readonly Dictionary<string, string> cookies = new Dictionary<string, string>();

        public void SetCookie(string key, string value)
        {
            cookies[key] = value;
        }

        public bool TryGetCookie(string key, out string value)
        {
            return cookies.TryGetValue(key, out value);
        }

        public void RemoveCookie(string key)
        {
            cookies.Remove(key);
        }

        public void ClearCookies()
        {
            cookies.Clear();
        }
    }
}
