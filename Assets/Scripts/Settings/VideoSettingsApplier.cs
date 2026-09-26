using UnityEngine;
using UnityEngine.SceneManagement;

namespace Interrogation.Settings
{
    /// <summary>
    /// A <see cref="VideoSettings"/> alkalmazása a motorra. Indításkor magától létrejön, és minden
    /// változásra újra alkalmaz.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class VideoSettingsApplier : MonoBehaviour
    {
        private static VideoSettingsApplier _instance;

        private RenderPipelineTuner _pipeline;
        private PostFxOverrides _postFx;
        private FpsCounter _fps;
        private Camera _camera;

        // Az Editorban a QualitySettings a projektbe íródna, ezért kilépéskor visszaállítjuk.
        private int _origVSync;
        private int _origFrameRate;
        private int _origMipLimit;
        private AnisotropicFiltering _origAniso;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            DisplaySettings.ApplySaved();
            if (_instance != null) return;
            var go = new GameObject(nameof(VideoSettingsApplier));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<VideoSettingsApplier>();
        }

        private void Awake()
        {
            _origVSync = QualitySettings.vSyncCount;
            _origFrameRate = Application.targetFrameRate;
            _origMipLimit = QualitySettings.globalTextureMipmapLimit;
            _origAniso = QualitySettings.anisotropicFiltering;

            _pipeline = RenderPipelineTuner.Create();
            _postFx = new PostFxOverrides(transform);
            _fps = gameObject.AddComponent<FpsCounter>();
            _fps.enabled = false;
        }

        private void OnEnable()
        {
            VideoSettings.Changed += ApplyAll;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            VideoSettings.Changed -= ApplyAll;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _postFx.CaptureBaseExposure();
            _camera = null;
            ApplyAll();
        }

        private void LateUpdate()
        {
            // A fő kamera cserélődhet (pl. jelenetváltás), az élsimítás vele megy.
            var main = Camera.main;
            if (main == _camera) return;
            _camera = main;
            RenderPipelineTuner.ApplyCamera(_camera);
        }

        private void ApplyAll()
        {
            QualitySettings.vSyncCount = VideoSettings.VSync.On ? 1 : 0;
            Application.targetFrameRate = VideoSettings.VSync.On ? -1 : VideoSettings.FpsCaps[VideoSettings.FpsCap.Value];

            QualitySettings.globalTextureMipmapLimit = VideoSettings.Textures.Value;
            var aniso = VideoSettings.Anisotropic.Value;
            QualitySettings.anisotropicFiltering = aniso == 0 ? AnisotropicFiltering.Disable : AnisotropicFiltering.ForceEnable;
            if (aniso > 0)
            {
                var level = 1 << aniso; // 2, 4, 8, 16
                Texture.SetGlobalAnisotropicFilteringLimits(level, level);
            }

            _pipeline?.Apply();
            _postFx.Apply();
            RenderPipelineTuner.ApplyCamera(Camera.main);
            _fps.enabled = VideoSettings.ShowFps.On;
        }

        private void OnApplicationQuit() => Restore();

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Restore()
        {
            QualitySettings.vSyncCount = _origVSync;
            Application.targetFrameRate = _origFrameRate;
            QualitySettings.globalTextureMipmapLimit = _origMipLimit;
            QualitySettings.anisotropicFiltering = _origAniso;
            _pipeline?.Restore();
            _pipeline = null;
            _postFx?.Dispose();
        }
    }
}
