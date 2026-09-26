using UnityEngine;

namespace Interrogation.Audio
{
    /// <summary>
    /// Egyszeri hangok lejátszója AudioSource-készlettel. A szünet a készlet hangjait is megállítja,
    /// ezért a felület hangjai <see cref="ignoreListenerPause"/> forrást kapnak.
    /// </summary>
    public sealed class SoundPlayer : MonoBehaviour
    {
        [SerializeField] [Min(1)] private int voices = 10;
        [Tooltip("Térbeli hangoknál a 3D-arány (0 = 2D, 1 = teljesen térbeli).")]
        [SerializeField] [Range(0f, 1f)] private float spatialBlend = 0.6f;
        [Tooltip("Igaz: a szünet alatt is szól (a felület hangjaihoz).")]
        [SerializeField] private bool ignoreListenerPause;

        private AudioSource[] _sources;
        private int _next;

        private void Awake()
        {
            _sources = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                var go = new GameObject($"Voice{i}");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.ignoreListenerPause = ignoreListenerPause;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 1.5f;
                src.maxDistance = 12f;
                src.dopplerLevel = 0f;
                _sources[i] = src;
            }
        }

        /// <summary>Visszaadja a klip hosszát másodpercben, vagy 0-t.</summary>
        public float Play(SoundCue cue, float bus, Vector3? position = null, float volumeScale = 1f)
        {
            if (cue == null || _sources == null) return 0f;
            var clip = cue.Pick();
            if (clip == null) return 0f;

            var src = NextFree();
            src.clip = clip;
            src.volume = Mathf.Clamp01(cue.volume * bus * volumeScale);
            src.pitch = cue.PickPitch();
            src.spatialBlend = position.HasValue ? spatialBlend : 0f;
            src.transform.position = position ?? transform.position;
            src.Play();
            return clip.length / Mathf.Max(0.01f, src.pitch);
        }

        // Ha mind szól, a legrégebben indított.
        private AudioSource NextFree()
        {
            for (int n = 0; n < _sources.Length; n++)
            {
                var i = (_next + n) % _sources.Length;
                if (_sources[i].isPlaying) continue;
                _next = (i + 1) % _sources.Length;
                return _sources[i];
            }
            var src = _sources[_next];
            _next = (_next + 1) % _sources.Length;
            return src;
        }
    }
}
