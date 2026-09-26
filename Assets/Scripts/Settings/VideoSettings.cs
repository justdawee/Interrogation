using System;
using UnityEngine;

namespace Interrogation.Settings
{
    public enum UpscalerKind { Off, Fsr1, Stp, Fsr2, Dlss }

    /// <summary>
    /// A grafikai és kijelző-beállítások adatmodellje; az alkalmazást a <see cref="VideoSettingsApplier"/>
    /// végzi. Ha a játékos egy preset-érintett beállítást módosít, a szint „Egyéni" lesz.
    /// </summary>
    public static class VideoSettings
    {
        public const int CustomPreset = 4;

        public static event Action Changed;

        private static void Notify()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static void NotifyDetail()
        {
            Preset.SetSilently(MatchingPreset());
            Notify();
        }

        // Kijelző
        public static readonly OptionSetting VSync = new OptionSetting("VSync", 1, Notify, "ki", "be");
        public static readonly int[] FpsCaps = { -1, 30, 60, 120, 144, 165, 240 };
        public static readonly OptionSetting FpsCap = new OptionSetting("FpsCap", 2, Notify,
            "korlátlan", "30", "60", "120", "144", "165", "240");

        // Grafika
        public static readonly OptionSetting Preset = new OptionSetting("Preset", 2, ApplyPresetFromMenu,
            "Alacsony", "Közepes", "Magas", "Ultra", "Egyéni");

        public static readonly OptionSetting Upscaler = new OptionSetting("Upscaler", 0, Notify,
            "ki (natív)", "FSR 1.0", "STP", "FSR 2", "DLSS 4");

        // A render-skála a térbeli felskálázóknál, a minőségi mód a DLSS/FSR 2-nél számít.
        public static readonly OptionSetting UpscaleMode = new OptionSetting("UpscaleMode", 0, Notify,
            "Minőség", "Kiegyensúlyozott", "Teljesítmény", "Ultra teljesítmény", "Natív");
        public static readonly float[] UpscaleRenderScales = { 0.67f, 0.58f, 0.5f, 0.33f, 1f };

        public static readonly OptionSetting AntiAliasing = new OptionSetting("AntiAliasing", 2, NotifyDetail,
            "ki", "FXAA", "SMAA", "TAA", "MSAA 2x", "MSAA 4x", "MSAA 8x");
        public static readonly OptionSetting Textures = new OptionSetting("Textures", 0, NotifyDetail,
            "Teljes", "Fél", "Negyed");
        public static readonly OptionSetting Anisotropic = new OptionSetting("Anisotropic", 3, NotifyDetail,
            "ki", "2x", "4x", "8x", "16x");
        public static readonly OptionSetting Shadows = new OptionSetting("Shadows", 2, NotifyDetail,
            "Alacsony", "Közepes", "Magas", "Ultra");
        public static readonly OptionSetting AmbientOcclusion = new OptionSetting("SSAO", 1, NotifyDetail, "ki", "be");

        // Effektek
        public static readonly OptionSetting Brightness = new OptionSetting("Brightness", 5, Notify,
            "-5", "-4", "-3", "-2", "-1", "0", "+1", "+2", "+3", "+4", "+5");
        public static readonly OptionSetting Bloom = new OptionSetting("Bloom", 1, Notify, "ki", "normál", "magas minőség");
        public static readonly OptionSetting FilmGrain = new OptionSetting("FilmGrain", 1, Notify, "ki", "be");
        public static readonly OptionSetting ChromaticAberration = new OptionSetting("Chromatic", 1, Notify, "ki", "be");
        public static readonly OptionSetting CameraMotion = new OptionSetting("CameraMotion", 0, Notify,
            "teljes", "csökkentett", "ki");
        public static readonly OptionSetting ShowFps = new OptionSetting("ShowFps", 0, Notify, "ki", "be");

        public static float CameraMotionScale => CameraMotion.Value == 0 ? 1f : CameraMotion.Value == 1 ? 0.4f : 0f;

        /// <summary>Expozíció-eltolás EV-ben.</summary>
        public static float BrightnessEv => (Brightness.Value - 5) * 0.1f;

        // Felskálázás

        /// <summary>A nem támogatott választás natívra esik vissza.</summary>
        public static UpscalerKind ActiveUpscaler
        {
            get
            {
                var kind = (UpscalerKind)Upscaler.Value;
                return UpscalerSupport.IsAvailable(kind) ? kind : UpscalerKind.Off;
            }
        }

        /// <summary>STP / FSR 2 / DLSS: saját élsimítást ad, az MSAA/FXAA/SMAA nem kell.</summary>
        public static bool TemporalUpscalerActive
        {
            get
            {
                var kind = ActiveUpscaler;
                return kind == UpscalerKind.Stp || kind == UpscalerKind.Fsr2 || kind == UpscalerKind.Dlss;
            }
        }

        public static void StepUpscaler(int delta)
        {
            var value = ActiveUpscaler == UpscalerKind.Off ? 0 : Upscaler.Value;
            for (int i = 0; i < Upscaler.Count; i++)
            {
                value = ((value + delta) % Upscaler.Count + Upscaler.Count) % Upscaler.Count;
                if (UpscalerSupport.IsAvailable((UpscalerKind)value)) break;
            }
            Upscaler.Value = value;
        }

        // Presetek
        // Sorrend: élsimítás, textúra, anizotróp, árnyék, SSAO.
        private static readonly int[][] Presets =
        {
            new[] { 1, 1, 0, 0, 0 }, // Alacsony: FXAA, fél textúra, ki, alacsony árnyék, SSAO ki
            new[] { 2, 0, 2, 1, 0 }, // Közepes: SMAA, 4x
            new[] { 2, 0, 3, 2, 1 }, // Magas:   SMAA, 8x, SSAO
            new[] { 5, 0, 4, 3, 1 }, // Ultra:   MSAA 4x, 16x
        };

        private static OptionSetting[] PresetMembers => new[] { AntiAliasing, Textures, Anisotropic, Shadows, AmbientOcclusion };

        private static void ApplyPresetFromMenu()
        {
            var preset = Preset.Value;
            if (preset < Presets.Length)
            {
                var members = PresetMembers;
                for (int i = 0; i < members.Length; i++) members[i].SetSilently(Presets[preset][i]);
            }
            Notify();
        }

        private static int MatchingPreset()
        {
            var members = PresetMembers;
            for (int p = 0; p < Presets.Length; p++)
            {
                var match = true;
                for (int i = 0; i < members.Length && match; i++) match = members[i].Value == Presets[p][i];
                if (match) return p;
            }
            return CustomPreset;
        }

        public static void ResetToDefaults()
        {
            foreach (var s in new[] { VSync, FpsCap, Preset, Upscaler, UpscaleMode, AntiAliasing, Textures, Anisotropic,
                         Shadows, AmbientOcclusion, Brightness, Bloom, FilmGrain, ChromaticAberration, CameraMotion, ShowFps })
                s.ResetSilently();
            Notify();
        }
    }
}
