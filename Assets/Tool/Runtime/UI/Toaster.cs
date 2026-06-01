using System;
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

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        public static void PushError(string title, Exception exception)
        {
            PushError(title, exception.Message);
        }

        public static void PushError(string title, string message)
        {
            Push(title, message, true);
        }

        public static void Push(string title, string message, bool error = false)
        {
            if (_instance == null)
            {
                if (error)
                    Debug.LogError($"{title}: {message}");
                else
                    Debug.Log($"{title}: {message}");
                return;
            }

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
