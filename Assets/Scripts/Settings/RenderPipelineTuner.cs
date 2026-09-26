using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Interrogation.Settings
{
    /// <summary>
    /// URP-oldali grafikai beállítások az URP-asset futásidejű klónján, így Play módban a lemezen lévő
    /// asset nem változik; kilépéskor minden visszaáll.
    /// </summary>
    public sealed class RenderPipelineTuner
    {
        private readonly struct ShadowTier
        {
            public readonly int Resolution;
            public readonly float Distance;
            public readonly int Cascades;

            public ShadowTier(int resolution, float distance, int cascades)
            {
                Resolution = resolution;
                Distance = distance;
                Cascades = cascades;
            }
        }

        // A kihallgatószoba néhány méteres: nagy árnyéktávolság csak a felbontást pazarolná.
        private static readonly ShadowTier[] ShadowTiers =
        {
            new ShadowTier(512, 12f, 1),
            new ShadowTier(1024, 18f, 2),
            new ShadowTier(2048, 25f, 2),
            new ShadowTier(4096, 35f, 4),
        };

        private readonly RenderPipelineAsset _originalQualityAsset;
        private readonly UniversalRenderPipelineAsset _asset;
        private readonly ScriptableRendererFeature _ssao;
        private readonly bool _ssaoOriginal;

        private RenderPipelineTuner(RenderPipelineAsset originalQualityAsset, UniversalRenderPipelineAsset clone,
            ScriptableRendererFeature ssao)
        {
            _originalQualityAsset = originalQualityAsset;
            _asset = clone;
            _ssao = ssao;
            _ssaoOriginal = ssao != null && ssao.isActive;
        }

        /// <summary>URP nélkül null.</summary>
        public static RenderPipelineTuner Create()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset source)) return null;

            var clone = UnityEngine.Object.Instantiate(source);
            clone.name = source.name + " (runtime)";
            clone.hideFlags = HideFlags.DontSave;
            CloneUpscalerOptions(clone);

            var original = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = clone;
            return new RenderPipelineTuner(original, clone, FindSsao(source));
        }

        public void Apply()
        {
            ApplyUpscaler();
            ApplyShadows();
            _asset.msaaSampleCount = VideoSettings.TemporalUpscalerActive ? 1 : MsaaSamples(VideoSettings.AntiAliasing.Value);
            if (_ssao != null) _ssao.SetActive(VideoSettings.AmbientOcclusion.On);
        }

        /// <summary>Időbeli felskálázónál a URP maga kapcsol TAA-ra.</summary>
        public static void ApplyCamera(Camera camera)
        {
            if (camera == null || !camera.TryGetComponent(out UniversalAdditionalCameraData data)) return;
            var aa = VideoSettings.AntiAliasing.Value;
            data.antialiasing = VideoSettings.TemporalUpscalerActive ? AntialiasingMode.None
                : aa == 1 ? AntialiasingMode.FastApproximateAntialiasing
                : aa == 2 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                : aa == 3 ? AntialiasingMode.TemporalAntiAliasing
                : AntialiasingMode.None;
            data.antialiasingQuality = AntialiasingQuality.High;
            camera.allowMSAA = true;
        }

        public void Restore()
        {
            if (_ssao != null) _ssao.SetActive(_ssaoOriginal);
            QualitySettings.renderPipeline = _originalQualityAsset;
            if (_asset != null) UnityEngine.Object.Destroy(_asset);
        }

        private void ApplyUpscaler()
        {
            var kind = VideoSettings.ActiveUpscaler;
            var mode = VideoSettings.UpscaleMode.Value;
            var qualityModeUpscaler = kind == UpscalerKind.Dlss || kind == UpscalerKind.Fsr2;
            // A DLSS / FSR 2 a saját minőségi módjából számolja a belső felbontást; a többinél a render-skála dönt.
            _asset.renderScale = kind == UpscalerKind.Off || qualityModeUpscaler ? 1f : VideoSettings.UpscaleRenderScales[mode];

#if ENABLE_UPSCALER_FRAMEWORK
            _asset.upscalerName = UpscalerSupport.PipelineName(kind);
            if (kind == UpscalerKind.Dlss)
                SetQualityMode(UpscalerSupport.DlssName, "dlssQualityMode",
                    new[] { "MaximumQuality", "Balanced", "MaximumPerformance", "UltraPerformance", "DLAA" }[mode]);
            else if (kind == UpscalerKind.Fsr2)
                SetQualityMode(UpscalerSupport.Fsr2Name, "fsr2QualityMode",
                    new[] { "Quality", "Balanced", "Performance", "UltraPerformance", "Quality" }[mode]);
#else
#pragma warning disable 618 // a keretrendszer nélkül a régi szűrő-választó az egyetlen út
            _asset.upscalingFilter = kind == UpscalerKind.Fsr1 ? UpscalingFilterSelection.FSR
                : kind == UpscalerKind.Stp ? UpscalingFilterSelection.STP
                : UpscalingFilterSelection.Auto;
#pragma warning restore 618
#endif
        }

        private void ApplyShadows()
        {
            var tier = ShadowTiers[VideoSettings.Shadows.Value];
            _asset.mainLightShadowmapResolution = tier.Resolution;
            _asset.additionalLightsShadowmapResolution = tier.Resolution;
            _asset.shadowDistance = tier.Distance;
            _asset.shadowCascadeCount = tier.Cascades;
        }

        private static int MsaaSamples(int aaIndex) => aaIndex == 4 ? 2 : aaIndex == 5 ? 4 : aaIndex == 6 ? 8 : 1;

#if ENABLE_UPSCALER_FRAMEWORK
        // Reflexióval, mert ezek a típusok csak a gyártói modulokkal léteznek.
        private void SetQualityMode(string upscalerName, string property, string enumName)
        {
            var options = _asset.GetUpscalerOptions(upscalerName);
            var prop = options?.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || !prop.PropertyType.IsEnum) return;
            if (Enum.IsDefined(prop.PropertyType, enumName))
                prop.SetValue(options, Enum.Parse(prop.PropertyType, enumName));
        }
#endif

        // Az opció-alassetek is klónozódnak, különben a minőségi mód a lemezre íródna.
        private static void CloneUpscalerOptions(UniversalRenderPipelineAsset clone)
        {
#if ENABLE_UPSCALER_FRAMEWORK
            var list = clone.upscalerOptions;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                var copy = UnityEngine.Object.Instantiate(list[i]);
                copy.hideFlags = HideFlags.DontSave;
                list[i] = copy;
            }
            AddMissingOptions(clone, "DLSSOptions", UpscalerSupport.DlssName);
            AddMissingOptions(clone, "FSR2Options", UpscalerSupport.Fsr2Name);
#endif
        }

#if ENABLE_UPSCALER_FRAMEWORK
        private static void AddMissingOptions(UniversalRenderPipelineAsset clone, string typeName, string upscalerName)
        {
            if (clone.GetUpscalerOptions(upscalerName) != null) return;
            var type = Type.GetType(typeName + ", Unity.RenderPipelines.Core.Runtime");
            if (type == null || !typeof(UpscalerOptions).IsAssignableFrom(type)) return;
            var options = (UpscalerOptions)ScriptableObject.CreateInstance(type);
            options.hideFlags = HideFlags.DontSave;
            options.upscalerName = upscalerName;
            clone.upscalerOptions.Add(options);
        }
#endif

        private static ScriptableRendererFeature FindSsao(UniversalRenderPipelineAsset asset)
        {
            foreach (var data in asset.rendererDataList)
            {
                if (data == null) continue;
                foreach (var feature in data.rendererFeatures)
                    if (feature != null && feature.GetType().Name == "ScreenSpaceAmbientOcclusion") return feature;
            }
            return null;
        }
    }
}
