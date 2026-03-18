using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class Toaster : MonoBehaviour
    {
        static Toaster _instance;

        [SerializeField] private AudioClip[] _toastSounds;
        [SerializeField] private AudioClip[] _toastSoundsError;
        [SerializeField] private Transform _parent;
        [SerializeField] private ToastEntry _prefab;

        private void Awake()
        {
            _instance = this;
        }

        public static void PushError(string title, string message)
        {
            _instance.InternalPush(title, message, true);
        }

        public static void Push(string title, string message, bool error = false)
        {
            _instance.InternalPush(title, message, error);
        }

        private void InternalPush(string title, string message, bool error)
        {
            Sounds2D.Play(new AudioSession(error ? _toastSoundsError : _toastSounds)
                .WithPitch(1f, 0.1f).WithVolume(0.1f));

            var entry = Instantiate(_prefab, _parent);
            entry.Setup(title, message, error);
        }
    }
}
