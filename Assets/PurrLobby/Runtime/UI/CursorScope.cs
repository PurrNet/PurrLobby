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
        /// <summary>Cursor access, swappable in tests (batchmode can't hold a locked cursor).</summary>
        internal interface ICursor
        {
            CursorLockMode lockState { get; set; }
            bool visible { get; set; }
        }

        sealed class UnityCursor : ICursor
        {
            public CursorLockMode lockState { get => Cursor.lockState; set => Cursor.lockState = value; }
            public bool visible { get => Cursor.visible; set => Cursor.visible = value; }
        }

        internal static readonly ICursor unityCursor = new UnityCursor();
        internal static ICursor cursor = unityCursor;

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
                _savedLock = cursor.lockState;
                _savedVisible = cursor.visible;
            }

            cursor.lockState = CursorLockMode.None;
            cursor.visible = true;
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

            if (cursor.lockState == CursorLockMode.None && cursor.visible)
            {
                cursor.lockState = _savedLock;
                cursor.visible = _savedVisible;
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

        /// <summary>
        /// Unlock and show the cursor unconditionally and forget any open scopes — used
        /// by the main menu, which always needs a free cursor no matter how the player
        /// got there (left, kicked, host gone). Scopes still closing afterwards are no-ops.
        /// </summary>
        public static void Release()
        {
            _depth = 0;
            cursor.lockState = CursorLockMode.None;
            cursor.visible = true;
        }
    }
}
