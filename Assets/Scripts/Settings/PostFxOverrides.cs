using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Interrogation.Settings
{
    /// <summary>
    /// A játékos effekt-beállításai egy legmagasabb prioritású Volume-ban. Csak a módosított paraméter kap
    /// felülírást, így a jelenet profiljainak többi effektje tovább működik.
    /// </summary>
    public sealed class PostFxOverrides
    {
        private const float Priority = 1000f;

        private readonly GameObject _host;
        private readonly VolumeProfile _profile;
        private readonly ColorAdjustments _color;
        private readonly Bloom _bloom;
        private readonly FilmGrain _grain;
        private readonly ChromaticAberration _chromatic;
        private float _baseExposure;

        public PostFxOverrides(Transform parent)
        {
            _host = new GameObject("SettingsPostFxOverrides");
            _host.transform.SetParent(parent, false);
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.hideFlags = HideFlags.DontSave;
            _color = _profile.Add<ColorAdjustments>();
            _bloom = _profile.Add<Bloom>();
            _grain = _profile.Add<FilmGrain>();
            _chromatic = _profile.Add<ChromaticAberration>();

            var volume = _host.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = Priority;
            volume.sharedProfile = _profile;
        }

        /// <summary>A fényerő a jelenet legerősebb globális Volume-jához képest tol.</summary>
        public void CaptureBaseExposure()
        {
            _baseExposure = 0f;
            var bestPriority = float.MinValue;
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude))
            {
                if (!v.isGlobal || v.sharedProfile == null || v.sharedProfile == _profile || v.priority < bestPriority) continue;
                if (v.sharedProfile.TryGet(out ColorAdjustments ca) && ca.postExposure.overrideState)
                {
                    _baseExposure = ca.postExposure.value;
                    bestPriority = v.priority;
                }
            }
        }

        public void Apply()
        {
            var ev = VideoSettings.BrightnessEv;
            _color.postExposure.overrideState = !Mathf.Approximately(ev, 0f);
            _color.postExposure.value = _baseExposure + ev;

            var bloom = VideoSettings.Bloom.Value;
            _bloom.intensity.overrideState = bloom == 0;
            _bloom.intensity.value = 0f;
            _bloom.highQualityFiltering.overrideState = true;
            _bloom.highQualityFiltering.value = bloom == 2;

            _grain.intensity.overrideState = !VideoSettings.FilmGrain.On;
            _grain.intensity.value = 0f;

            _chromatic.intensity.overrideState = !VideoSettings.ChromaticAberration.On;
            _chromatic.intensity.value = 0f;
        }

        public void Dispose()
        {
            if (_host != null) Object.Destroy(_host);
            if (_profile != null) Object.Destroy(_profile);
        }
    }
}
