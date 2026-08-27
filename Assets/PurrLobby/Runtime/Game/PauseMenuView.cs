using System;
using System.Collections;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class PauseMenuView : MonoView
    {
        /// <summary>
        /// Raised whenever a pause menu opens/closes, no matter which code path
        /// pushed or popped it. Consumer games can hook these to disable player input.
        /// </summary>
        public static event Action onOpened;
        public static event Action onClosed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            onOpened = null;
            onClosed = null;
        }

        private bool _leaving;
        private bool _cursorScopeOpen;

        public override void OnPushed()
        {
            _cursorScopeOpen = true;
            CursorScope.PushUnlocked();
            onOpened?.Invoke();
        }

        public override void OnPopped()
        {
            CloseCursorScope();
            onClosed?.Invoke();
        }

        private void OnDestroy()
        {
            CloseCursorScope();
        }

        private void CloseCursorScope()
        {
            if (!_cursorScopeOpen)
                return;

            _cursorScopeOpen = false;

            // When the scene itself is going away (leave to menu, game over, connection
            // lost) the saved in-game cursor state is obsolete — the menu needs a free
            // cursor, so drop the scope without restoring.
            if (_leaving || !gameObject.scene.isLoaded)
                CursorScope.PopAbandon();
            else
                CursorScope.PopRestore();
        }

        public void Resume()
        {
            PopMe();
        }

        public void LeaveToMenu()
        {
            if (_leaving)
                return;

            if (!GameSession.TryGet(gameObject.scene, out var session))
                session = GameSession.instance;

            if (!session)
            {
                Toaster.PushError("Cannot leave", "No GameSession found in this scene.");
                return;
            }

            _leaving = true;
            canvasGroup.interactable = false;
            session.LeaveToMenu();
        }

        public void QuitToDesktop()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        protected override IEnumerator OnEnterTransition() => ViewTransitions.FadeIn(this, 0.1f);

        protected override IEnumerator OnExitTransition() => ViewTransitions.FadeOut(this, 0.1f);
    }
}
