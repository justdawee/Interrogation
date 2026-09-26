using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Interrogation.Settings
{
    /// <summary>
    /// Melyik felskálázó használható: FSR 1.0 mindig, STP compute shaderrel, a DLSS és az FSR 2 a URP
    /// felskálázó-keretrendszerén át (a DLSS csak NVIDIA RTX-en).
    /// </summary>
    public static class UpscalerSupport
    {
        public const string BilinearName = "Bilinear";
        public const string Fsr1Name = "FidelityFX Super Resolution 1.0";
        public const string StpName = "Spatial-Temporal Post-Processing";
        public const string Fsr2Name = "FidelityFX Super Resolution 2";
        public const string DlssName = "Deep Learning Super Sampling 4";

        private static bool? _dlssHardware;

        public static bool FrameworkEnabled
        {
            get
            {
#if ENABLE_UPSCALER_FRAMEWORK
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsAvailable(UpscalerKind kind)
        {
            switch (kind)
            {
                case UpscalerKind.Off:
                case UpscalerKind.Fsr1: return true;
                case UpscalerKind.Stp: return SystemInfo.supportsComputeShaders;
                case UpscalerKind.Fsr2: return IsRegistered(Fsr2Name) && SystemInfo.supportsComputeShaders;
                case UpscalerKind.Dlss: return IsRegistered(DlssName) && DlssHardware;
                default: return false;
            }
        }

        public static string PipelineName(UpscalerKind kind)
        {
            switch (kind)
            {
                case UpscalerKind.Fsr1: return Fsr1Name;
                case UpscalerKind.Stp: return StpName;
                case UpscalerKind.Fsr2: return Fsr2Name;
                case UpscalerKind.Dlss: return DlssName;
                default: return BilinearName;
            }
        }

        private static bool IsRegistered(string name)
        {
#if ENABLE_UPSCALER_FRAMEWORK
            var pipeline = RenderPipelineManager.currentPipeline as UniversalRenderPipeline;
            return pipeline != null && pipeline.availableUpscalerNames.Contains(name);
#else
            return false;
#endif
        }

        // Reflexióval, hogy a kód az NVIDIA-modul nélkül is forduljon.
        private static bool DlssHardware => _dlssHardware ??= ProbeDlss();

        private static bool ProbeDlss()
        {
            if (!SystemInfo.graphicsDeviceVendor.ToLowerInvariant().Contains("nvidia")) return false;
            try
            {
                var plugin = Type.GetType("UnityEngine.NVIDIA.NVUnityPlugin, UnityEngine.NVIDIAModule");
                var deviceType = Type.GetType("UnityEngine.NVIDIA.GraphicsDevice, UnityEngine.NVIDIAModule");
                var featureType = Type.GetType("UnityEngine.NVIDIA.GraphicsDeviceFeature, UnityEngine.NVIDIAModule");
                if (plugin == null || deviceType == null || featureType == null) return false;

                var loaded = plugin.GetMethod("IsLoaded", BindingFlags.Public | BindingFlags.Static);
                if (loaded == null || !(bool)loaded.Invoke(null, null)) return false;

                var create = deviceType.GetMethod("CreateGraphicsDevice", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                var device = create?.Invoke(null, null);
                var isAvailable = deviceType.GetMethod("IsFeatureAvailable", BindingFlags.Public | BindingFlags.Instance);
                if (device == null || isAvailable == null) return false;
                return (bool)isAvailable.Invoke(device, new[] { Enum.Parse(featureType, "DLSS") });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UpscalerSupport] A DLSS-támogatás nem ellenőrizhető: {e.Message}");
                return false;
            }
        }
    }
}
