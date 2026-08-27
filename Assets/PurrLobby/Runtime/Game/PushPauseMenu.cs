using PurrNet.UI;
using UnityEngine;
using UnityEngine.Events;

namespace PurrNet.Lobby
{
    /// <summary>
    /// Sole Escape owner in the game scene: opens the pause menu when nothing is
    /// on the stack, closes it when it is topmost, and defers to whatever other
    /// view is topmost (its own back handler owns the press).
    /// </summary>
    public class PushPauseMenu : MonoBehaviour
    {
        [SerializeField] private ViewStack _stack;

        [SerializeField] private UnityEvent _onPauseOpened;
        [SerializeField] private UnityEvent _onPauseClosed;

        private void OnEnable()
        {
            PauseMenuView.onOpened += OnPauseOpened;
            PauseMenuView.onClosed += OnPauseClosed;
        }

        private void OnDisable()
        {
            PauseMenuView.onOpened -= OnPauseOpened;
            PauseMenuView.onClosed -= OnPauseClosed;
        }

        private void OnPauseOpened() => _onPauseOpened?.Invoke();

        private void OnPauseClosed() => _onPauseClosed?.Invoke();

        private void Update()
        {
            var top = _stack.top;

            if (top is PauseMenuView pause)
            {
                if ((BackInput.WasPausePressed() || BackInput.WasBackPressed()) && BackInput.TryConsume())
                    _stack.Pop(pause);
            }
            else if (top == null)
            {
                if (BackInput.WasPausePressed() && BackInput.TryConsume())
                    _stack.Push<PauseMenuView>();
            }
            // else: another view (context menu, dialog, consumer view) is topmost.
            // Its own back handler owns this press; the BackInput latch guarantees
            // no double-consume regardless of Update order.
        }
    }
}
