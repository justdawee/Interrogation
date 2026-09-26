using Interrogation.UI;
using UnityEngine;

namespace Interrogation.Audio
{
    /// <summary>
    /// Zenelejátszó két forrással, keresztáttűnéssel. A <see cref="Duck"/> ideiglenesen lehalkít
    /// (pl. amíg a gyanúsított gondolkodik).
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private float duckSpeed = 1.5f;

        private readonly Channel[] _channels = new Channel[2];
        private int _active;
        private float _duck = 1f;

        public float Duck { get; set; } = 1f;

        private sealed class Channel
        {
            public AudioSource Source;
            public SoundChannel Bus;
            public float Gain;
            public float Target;
            public float Speed = 1f;
        }

        private void Awake()
        {
            for (int i = 0; i < _channels.Length; i++)
            {
                var go = new GameObject($"Music{i}");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.loop = true;
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.priority = 0;
                _channels[i] = new Channel { Source = src };
            }
        }

        /// <summary>A már szóló számot nem indítja újra.</summary>
        public void Play(AudioClip clip, SoundChannel bus, float fadeSeconds)
        {
            if (clip == null) { Stop(fadeSeconds); return; }
            var speed = 1f / Mathf.Max(0.05f, fadeSeconds);
            var current = _channels[_active];
            if (current.Source.clip == clip && current.Source.isPlaying)
            {
                current.Bus = bus;
                current.Target = 1f;
                current.Speed = speed;
                return;
            }

            current.Target = 0f;
            current.Speed = speed;
            _active = 1 - _active;
            var next = _channels[_active];
            next.Source.clip = clip;
            next.Source.time = 0f;
            next.Source.Play();
            next.Bus = bus;
            next.Gain = 0f;
            next.Target = 1f;
            next.Speed = speed;
        }

        public void Stop(float fadeSeconds)
        {
            var speed = 1f / Mathf.Max(0.05f, fadeSeconds);
            foreach (var c in _channels) { c.Target = 0f; c.Speed = speed; }
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _duck = Mathf.MoveTowards(_duck, Duck, duckSpeed * dt);
            foreach (var c in _channels)
            {
                c.Gain = Mathf.MoveTowards(c.Gain, c.Target, c.Speed * dt);
                c.Source.volume = Mathf.Clamp01(c.Gain * _duck * GameSettings.Volume(c.Bus));
                if (c.Gain <= 0.001f && c.Target <= 0f && c.Source.isPlaying) c.Source.Stop();
            }
        }
    }
}
