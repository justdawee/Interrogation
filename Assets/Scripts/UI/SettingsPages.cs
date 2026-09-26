using System;
using System.Collections.Generic;
using Interrogation.Settings;

namespace Interrogation.UI
{
    public enum SettingsPage { Hub, Display, Graphics, Effects, Audio, Gameplay }

    /// <summary>Az oldalak sorai; oldalanként legfeljebb 9, hogy görgetés nélkül elférjenek.</summary>
    public static class SettingsPages
    {
        public static string Title(SettingsPage page)
        {
            switch (page)
            {
                case SettingsPage.Display: return "KIJELZŐ";
                case SettingsPage.Graphics: return "GRAFIKA";
                case SettingsPage.Effects: return "EFFEKTEK";
                case SettingsPage.Audio: return "HANG";
                case SettingsPage.Gameplay: return "JÁTÉKMENET";
                default: return "BEÁLLÍTÁSOK";
            }
        }

        public static List<MenuEntry> Hub(Action<SettingsPage> open, Func<string> resetLabel, Action reset, Action back) =>
            new List<MenuEntry>
            {
                new MenuEntry("Kijelző", () => open(SettingsPage.Display)),
                new MenuEntry("Grafika", () => open(SettingsPage.Graphics)),
                new MenuEntry("Effektek", () => open(SettingsPage.Effects)),
                new MenuEntry("Hang", () => open(SettingsPage.Audio)),
                new MenuEntry("Játékmenet", () => open(SettingsPage.Gameplay)),
                new MenuEntry(resetLabel, reset),
                new MenuEntry("Vissza", back),
            };

        public static List<MenuEntry> Display(Action back) => new List<MenuEntry>
        {
            new MenuEntry(() => Row("Megjelenítés", DisplaySettings.PendingModeLabel), null, DisplaySettings.StepMode),
            new MenuEntry(() => Row("Felbontás", DisplaySettings.PendingResolutionLabel), null, DisplaySettings.StepResolution),
            new MenuEntry(() => DisplaySettings.IsDirty ? "Alkalmaz" : "Alkalmaz  <size=70%>(nincs változás)</size>",
                DisplaySettings.Apply, enabled: () => DisplaySettings.IsDirty),
            Option("V-Sync", VideoSettings.VSync),
            new MenuEntry(() => VideoSettings.VSync.On ? Row("FPS-korlát", "V-Sync") : Row("FPS-korlát", VideoSettings.FpsCap.Label),
                null, VideoSettings.FpsCap.Step, () => !VideoSettings.VSync.On),
            new MenuEntry("Vissza", back),
        };

        public static List<MenuEntry> Graphics(Action back) => new List<MenuEntry>
        {
            Option("Minőségi szint", VideoSettings.Preset),
            new MenuEntry(() => Row("Felskálázás", VideoSettings.Upscaler.Labels[(int)VideoSettings.ActiveUpscaler]),
                null, VideoSettings.StepUpscaler),
            new MenuEntry(() => VideoSettings.ActiveUpscaler == UpscalerKind.Off
                    ? Row("Felskálázási mód", "—") : Row("Felskálázási mód", UpscaleModeLabel()),
                null, VideoSettings.UpscaleMode.Step, () => VideoSettings.ActiveUpscaler != UpscalerKind.Off),
            new MenuEntry(() => VideoSettings.TemporalUpscalerActive
                    ? Row("Élsimítás", "a felskálázóé") : Row("Élsimítás", VideoSettings.AntiAliasing.Label),
                null, VideoSettings.AntiAliasing.Step, () => !VideoSettings.TemporalUpscalerActive),
            Option("Textúra-felbontás", VideoSettings.Textures),
            Option("Anizotróp szűrés", VideoSettings.Anisotropic),
            Option("Árnyékok", VideoSettings.Shadows),
            Option("Környezeti takarás", VideoSettings.AmbientOcclusion),
            new MenuEntry("Vissza", back),
        };

        public static List<MenuEntry> Effects(Action back) => new List<MenuEntry>
        {
            Option("Fényerő", VideoSettings.Brightness),
            Option("Ragyogás (bloom)", VideoSettings.Bloom),
            Option("Filmszemcse", VideoSettings.FilmGrain),
            Option("Kromatikus aberráció", VideoSettings.ChromaticAberration),
            Option("Kamera-mozgás", VideoSettings.CameraMotion),
            new MenuEntry("Vissza", back),
        };

        public static List<MenuEntry> Audio(Action back) => new List<MenuEntry>
        {
            Volume("Fő hangerő", SoundChannel.Master),
            Volume("Menüzene", SoundChannel.MenuMusic),
            Volume("Játékzene", SoundChannel.GameMusic),
            Volume("Hanghatások", SoundChannel.Sfx),
            Volume("Háttérzaj", SoundChannel.Ambient),
            Volume("Beszédhang", SoundChannel.Voice),
            new MenuEntry("Vissza", back),
        };

        public static List<MenuEntry> Gameplay(Action back) => new List<MenuEntry>
        {
            new MenuEntry(() => Row("Felirat-tempó", GameSettings.SpeedLabels[GameSettings.SpeedIndex]), null,
                d => GameSettings.SpeedIndex = Wrap(GameSettings.SpeedIndex + d, GameSettings.SpeedLabels.Length)),
            Option("FPS-kijelzés", VideoSettings.ShowFps),
            new MenuEntry("Vissza", back),
        };

        // A DLSS natív módja DLAA; az FSR 2-nek nincs natív módja.
        private static string UpscaleModeLabel()
        {
            var mode = VideoSettings.UpscaleMode.Value;
            var kind = VideoSettings.ActiveUpscaler;
            if (mode == 4 && kind == UpscalerKind.Dlss) return "DLAA (natív)";
            if (mode == 4 && kind == UpscalerKind.Fsr2) return "Minőség";
            if (kind == UpscalerKind.Dlss || kind == UpscalerKind.Fsr2) return VideoSettings.UpscaleMode.Label;
            return $"{VideoSettings.UpscaleMode.Label} ({VideoSettings.UpscaleRenderScales[mode] * 100f:0}%)";
        }

        private static MenuEntry Option(string label, OptionSetting setting) =>
            new MenuEntry(() => Row(label, setting.Label), null, setting.Step);

        private static MenuEntry Volume(string label, SoundChannel channel) =>
            new MenuEntry(() => Row(label, $"{GameSettings.GetVolumeStep(channel) * 10}%"), null,
                d => GameSettings.SetVolumeStep(channel, Wrap(GameSettings.GetVolumeStep(channel) + d, 11)));

        private static string Row(string label, string value) => $"{label}:  < {value} >";

        private static int Wrap(int value, int count) => ((value % count) + count) % count;
    }
}
