using UnityEngine;

namespace PurrLobby
{
    public struct AudioSession
    {
        public readonly AudioClip clip;

        public readonly AudioClip[] clips;

        public float? volume;

        public float? pitch;

        public AudioSession(AudioClip[] clips)
        {
            this.clips = clips;
            this.clip = null;
            this.volume = null;
            this.pitch = null;
        }


        public AudioSession(AudioClip clip)
        {
            this.clip = clip;
            this.clips = null;
            this.volume = null;
            this.pitch = null;
        }

        public AudioSession WithVolume(float volume)
        {
            var session = this;
            session.volume = volume;
            return session;
        }

        public AudioSession WithVolume(float volume, float randomRange)
        {
            var session = this;
            session.volume = volume + Random.Range(-randomRange, randomRange);
            return session;
        }

        public AudioSession WithPitch(float pitch)
        {
            var session = this;
            session.pitch = pitch;
            return session;
        }

        public AudioSession WithPitch(float pitch, float randomRange)
        {
            var session = this;
            session.pitch = pitch + Random.Range(-randomRange, randomRange);
            return session;
        }
    }
}
