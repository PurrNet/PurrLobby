using System.Collections.Generic;
using UnityEngine;

namespace PurrLobby
{
    public class Sounds2D : MonoBehaviour
    {
        const int MAX_AUDIO_SOURCES = 5;

        private static Sounds2D _me;

        private readonly List<AudioSource> _audioSources = new();

        public static float masterVolume = 1f;

        static Sounds2D GetMe()
        {
            if (_me)
                return _me;

            var go = new GameObject("Sounds2D");
            DontDestroyOnLoad(go);
            _me = go.AddComponent<Sounds2D>();
            return _me;
        }

        AudioSource GetAudioSource()
        {
            for (int i = 0; i < _audioSources.Count; i++)
            {
                if (!_audioSources[i].isPlaying)
                    return _audioSources[i];
            }

            if (_audioSources.Count < MAX_AUDIO_SOURCES)
            {
                var source = gameObject.AddComponent<AudioSource>();
                _audioSources.Add(source);
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                return source;
            }

            return null;
        }

        public static void Play(AudioSession sesion) => GetMe().PlayInternal(sesion);

        private void PlayInternal(AudioSession sesion)
        {
            if (!sesion.clip && (sesion.clips == null || sesion.clips.Length == 0))
                return;

            var source = GetAudioSource();

            if (source)
            {
                source.clip = sesion.clip ? sesion.clip : sesion.clips[Random.Range(0, sesion.clips.Length)];
                source.volume = (sesion.volume ?? 1f) * masterVolume;
                source.pitch = sesion.pitch ?? 1f;
                source.Play();
            }
        }
    }
}
