using UnityEngine;

namespace Interrogation.Audio
{
    /// <summary>
    /// Folyamatosan szóló hang-réteg; a hangerő és a hangmagasság a célértékhez simul.
    /// Nulla hangerőnél a forrás megáll.
    /// </summary>
    public sealed class LoopLayer
    {
        private readonly AudioSource _source;
        private readonly bool _randomStart;
        private float _volume;

        public float TargetVolume { get; set; }
        public float TargetPitch { get; set; } = 1f;
        public float FadeSpeed { get; set; } = 0.5f;

        public LoopLayer(GameObject host, string name, AudioClip clip, float spatialBlend = 0f, bool randomStart = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(host.transform, false);
            _source = go.AddComponent<AudioSource>();
            _source.clip = clip;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.volume = 0f;
            _source.spatialBlend = spatialBlend;
            _source.dopplerLevel = 0f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.minDistance = 1.5f;
            _source.maxDistance = 12f;
            _randomStart = randomStart;
        }

        public Transform Transform => _source.transform;

        /// <summary>Azonnali hangerő, hogy induláskor ne a csendből ússzon be.</summary>
        public void Snap(float volume, float bus)
        {
            TargetVolume = _volume = volume;
            Apply(bus);
        }

        public void Tick(float dt, float bus)
        {
            _volume = Mathf.MoveTowards(_volume, TargetVolume, FadeSpeed * dt);
            _source.pitch = Mathf.MoveTowards(_source.pitch, TargetPitch, dt);
            Apply(bus);
        }

        private void Apply(float bus)
        {
            if (_source.clip == null) return;
            _source.volume = Mathf.Clamp01(_volume * bus);
            if (_volume > 0.001f && !_source.isPlaying)
            {
                _source.Play();
                if (_randomStart) _source.time = Random.Range(0f, _source.clip.length * 0.9f);
            }
            else if (_volume <= 0.001f && _source.isPlaying) _source.Stop();
        }
    }
}
