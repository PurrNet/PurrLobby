using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PurrNet.Lobby
{
    /// <summary>
    /// Shared back/pause input polling plus a per-frame consumption latch so a
    /// single press is never handled by two components in the same frame.
    /// </summary>
    public static class BackInput
    {
        static int _lastConsumedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _lastConsumedFrame = -1;

        /// <summary>
        /// Call only when you are about to act on the press. The first caller in a frame wins.
        /// </summary>
        public static bool TryConsume()
        {
            if (_lastConsumedFrame == Time.frameCount)
                return false;

            _lastConsumedFrame = Time.frameCount;
            return true;
        }

        /// <summary>
        /// Escape on keyboard, or East/B on gamepad — the "close the current window" chord.
        /// </summary>
        public static bool WasBackPressed()
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

        /// <summary>
        /// Escape on keyboard, or Start/Menu on gamepad — the "open the pause menu" chord.
        /// </summary>
        public static bool WasPausePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var gp = Gamepad.current;

            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                return true;

            if (gp != null && gp.startButton.wasPressedThisFrame)
                return true;

            return false;
#else
            if (Input.GetKeyDown(KeyCode.Escape))
                return true;

            if (Input.GetKeyDown(KeyCode.JoystickButton7))
                return true;

            return false;
#endif
        }
    }
}
