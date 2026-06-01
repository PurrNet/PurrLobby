using PurrNet.UI;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PurrNet.Lobby
{
    public class PushPauseMenu : MonoBehaviour
    {
        [SerializeField] private ViewStack _stack;

        static bool WasEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private void Update()
        {
            if (WasEscapePressed())
            {
                var existing = _stack.GetFirstView<PauseMenuView>();
                if (existing)
                {
                    _stack.Pop(existing);
                }
                else
                {
                    _stack.Push<PauseMenuView>();
                }
            }
        }
    }
}
