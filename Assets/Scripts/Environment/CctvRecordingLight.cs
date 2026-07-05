using UnityEngine;

namespace Interrogation.Rendering
{
    /// <summary>
    /// A CCTV-kamera „felvétel" LED-je: rövid felvillanás, hosszabb szünet. Play módban fut;
    /// kikapcsoláskor visszaállítja az eredeti fényerőt.
    /// </summary>
    [RequireComponent(typeof(Light))]
    [DisallowMultipleComponent]
    public sealed class CctvRecordingLight : MonoBehaviour
    {
        [Header("Brightness")]
        [Tooltip("Light intensity during the visible flash (the LED 'on' state).")]
        [Min(0f)] public float onIntensity = 0.03f;
        [Tooltip("Light intensity during the gap between flashes (0 = fully off).")]
        [Min(0f)] public float offIntensity = 0f;

        [Header("Timing (seconds)")]
        [Tooltip("How long each flash stays lit.")]
        [Min(0.01f)] public float onDuration = 0.16f;
        [Tooltip("How long the LED stays dim between flashes.")]
        [Min(0.01f)] public float offDuration = 1.7f;
        [Tooltip("Random offset applied once at start so the blink is not perfectly regular.")]
        [Range(0f, 2f)] public float startJitter = 0.5f;

        [Header("Feel")]
        [Tooltip("Softly fade between on/off instead of snapping instantly.")]
        public bool smooth = true;
        [Tooltip("Higher = snappier transition when Smooth is on.")]
        [Range(1f, 40f)] public float fadeSpeed = 16f;

        Light _light;
        float _timer;
        float _cycle;
        float _restoreIntensity;

        void OnEnable()
        {
            _light = GetComponent<Light>();
            _restoreIntensity = _light.intensity;
            _cycle = Mathf.Max(0.02f, onDuration + offDuration);
            _timer = Random.Range(0f, startJitter);
            _light.intensity = offIntensity;
        }

        void OnDisable()
        {
            if (_light != null)
                _light.intensity = _restoreIntensity;
        }

        void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= _cycle)
                _timer -= _cycle;

            float target = _timer < onDuration ? onIntensity : offIntensity;

            _light.intensity = smooth
                ? Mathf.Lerp(_light.intensity, target, 1f - Mathf.Exp(-fadeSpeed * Time.deltaTime))
                : target;
        }
    }
}
