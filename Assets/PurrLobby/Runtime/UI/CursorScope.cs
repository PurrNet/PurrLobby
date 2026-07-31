using UnityEngine;

namespace PurrNet.Lobby
{
    /// <summary>
    /// Depth-counted cursor unlock used by overlay menus. The outermost
    /// <see cref="PushUnlocked"/> saves the game's cursor state; the matching
    /// <see cref="PopRestore"/> restores it — unless something else changed the
    /// cursor in the meantime, in which case that change wins.
    /// </summary>
    public static class CursorScope
    {
        static int _depth;
        static CursorLockMode _savedLock;
        static bool _savedVisible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _depth = 0;

        /// <summary>
        /// Unlock and show the cursor, saving the prior state on the first (outermost) call.
        /// </summary>
        public static void PushUnlocked()
        {
            if (_depth++ == 0)
            {
                _savedLock = Cursor.lockState;
                _savedVisible = Cursor.visible;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// Restore the saved state when the last scope closes — only if the cursor
        /// is still in the state we put it in.
        /// </summary>
        public static void PopRestore()
        {
            if (_depth == 0)
                return;

            if (--_depth > 0)
                return;

            if (Cursor.lockState == CursorLockMode.None && Cursor.visible)
            {
                Cursor.lockState = _savedLock;
                Cursor.visible = _savedVisible;
            }
        }

        /// <summary>
        /// Close a scope without restoring — used when the whole scene is going away
        /// (e.g. leaving to the menu) and the saved in-game cursor state is obsolete.
        /// </summary>
        public static void PopAbandon()
        {
            if (_depth > 0)
                _depth--;
        }
    }
}
