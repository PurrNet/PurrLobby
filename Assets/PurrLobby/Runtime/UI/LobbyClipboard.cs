#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace PurrNet.Lobby
{
    /// <summary>
    /// Copies text to the system clipboard. In WebGL builds GUIUtility.systemCopyBuffer
    /// doesn't reach the browser clipboard, so this goes through the browser clipboard API.
    /// Call it from a UI click handler: browsers only allow clipboard writes after a user gesture.
    /// </summary>
    public static class LobbyClipboard
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void PurrLobby_CopyToClipboard(string text);
#endif

        public static void Copy(string text)
        {
            text ??= string.Empty;
#if UNITY_WEBGL && !UNITY_EDITOR
            PurrLobby_CopyToClipboard(text);
#else
            GUIUtility.systemCopyBuffer = text;
#endif
        }
    }
}
