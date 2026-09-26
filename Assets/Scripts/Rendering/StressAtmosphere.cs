using Interrogation.Cameras;
using Interrogation.Character;
using Interrogation.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace Interrogation.Rendering
{
    /// <summary>
    /// Két globális Volume súlya az alap-profil fölött: a „stressz" a stresszel nő, az „összeomlás"
    /// (deszaturáció) összeomláskor úszik be, ekkor a kulcsfény is vibrál.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StressAtmosphere : MonoBehaviour
    {
        [Header("Hivatkozások")]
        [SerializeField] private SuspectMood mood;
        [SerializeField] private CameraDirector cameraDirector;
        [SerializeField] private InterrogationManager manager;
        [SerializeField] private Volume stressVolume;
        [SerializeField] private Volume breakingVolume;

        [Header("Stressz")]
        [Tooltip("A stressz-érték simítása (mp).")]
        [SerializeField] private float stressSmoothing = 1.2f;
        [Tooltip("Görbe-kitevő: 1 felett a hatás főleg a magas stressznél erősödik.")]
        [SerializeField] [Range(0.5f, 3f)] private float stressExponent = 1.4f;

        [Header("Összeomlás")]
        [SerializeField] private float breakingFadeIn = 4f;
        [SerializeField] private float breakingFadeOut = 1.5f;

        [Header("Kulcsfény-vibrálás (összeomlás alatt)")]
        [SerializeField] private Light keyLight;
        [Tooltip("A fényerő legnagyobb relatív esése (0..1).")]
        [SerializeField] [Range(0f, 0.8f)] private float flickerDepth = 0.3f;
        [SerializeField] private float flickerSpeed = 7f;

        private float _stress;
        private float _stressVelocity;
        private float _breaking;
        private float _keyBase;
        private bool _flickering;

        private void Awake()
        {
            if (keyLight != null) _keyBase = keyLight.intensity;
            SetWeight(stressVolume, 0f);
            SetWeight(breakingVolume, 0f);
        }

        private void OnDisable() => StopFlicker();

        private void Update()
        {
            var goal = mood != null ? mood.Stress01 : 0f;
            _stress = Mathf.SmoothDamp(_stress, goal, ref _stressVelocity, stressSmoothing);
            SetWeight(stressVolume, Mathf.Pow(Mathf.Clamp01(_stress), stressExponent));

            var breaking = cameraDirector != null && cameraDirector.IsBreaking;
            var fade = breaking ? breakingFadeIn : breakingFadeOut;
            _breaking = Mathf.MoveTowards(_breaking, breaking ? 1f : 0f, Time.deltaTime / Mathf.Max(0.05f, fade));
            SetWeight(breakingVolume, _breaking);

            // A befejezés fényeit már a rendező vezérli.
            if (breaking && (manager == null || !manager.IsOver)) Flicker();
            else StopFlicker();
        }

        private void Flicker()
        {
            if (keyLight == null) return;
            _flickering = true;
            var t = Time.time * flickerSpeed;
            // Lassú „lélegzés" + ritka, éles kihagyás (hibás izzó).
            var slow = Mathf.PerlinNoise(t * 0.35f, 0.3f);
            var fast = Mathf.PerlinNoise(t, 7.1f);
            var dip = Mathf.Clamp01((fast - 0.62f) * 4f);
            var drop = Mathf.Clamp01(0.35f * slow + 0.65f * dip) * flickerDepth * _breaking;
            keyLight.intensity = _keyBase * (1f - drop);
        }

        private void StopFlicker()
        {
            if (!_flickering || keyLight == null) return;
            _flickering = false;
            keyLight.intensity = _keyBase;
        }

        private static void SetWeight(Volume volume, float weight)
        {
            if (volume != null) volume.weight = weight;
        }
    }
}
