using PurrNet.UI;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PurrNet.Lobby
{
    public class PopWindowWithBackButton : MonoBehaviour
    {
        [SerializeField] private AudioClip[] _closeSounds;

        private void Update()
        {
            if ( WasBackPressed())
            {
                var parentWindow = GetComponentInParent<MonoView>();
                if (parentWindow.isTopMost)
                {
                    Sounds2D.Play(new AudioSession(_closeSounds).WithPitch(1, 0.1f));
                    parentWindow.PopMe();
                }
            }
        }

        static bool WasBackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var gp = Gamepad.current;

            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                return true;

            if (gp != null && gp.buttonEast.wasPressedThisFrame)
                return true;

            return false;
#else
            if (Input.GetKeyDown(KeyCode.Escape))
                return true;

            if (Input.GetKeyDown(KeyCode.JoystickButton1))
                return true;

            return false;
#endif
        }
    }
}
