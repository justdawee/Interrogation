using System.Collections.Generic;
using UnityEngine;

namespace Interrogation.Settings
{
    /// <summary>
    /// Megjelenítési mód és felbontás. A menü csak függőben állítja, a képernyő az „Alkalmaz" sorra vált,
    /// így léptetés közben nem villog az ablak.
    /// </summary>
    public static class DisplaySettings
    {
        private const string ModeKey = "Interrogation.Video.DisplayMode";
        private const string WidthKey = "Interrogation.Video.Width";
        private const string HeightKey = "Interrogation.Video.Height";
        private const string LegacyFullscreenKey = "Interrogation.Fullscreen";

        public static readonly string[] ModeLabels = { "keret nélküli ablak", "teljes képernyő", "ablakos" };
        private static readonly FullScreenMode[] Modes =
            { FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen, FullScreenMode.Windowed };

        private static readonly List<Resolution> ResolutionList = new List<Resolution>();

        public static int PendingMode { get; private set; }
        public static int PendingResolution { get; private set; }

        public static IReadOnlyList<Resolution> Resolutions
        {
            get
            {
                if (ResolutionList.Count == 0) BuildResolutions();
                return ResolutionList;
            }
        }

        public static string PendingModeLabel => ModeLabels[PendingMode];

        public static string PendingResolutionLabel
        {
            get
            {
                var r = Resolutions[PendingResolution];
                return $"{r.width} × {r.height}";
            }
        }

        public static bool IsDirty => PendingMode != CurrentModeIndex() || PendingResolution != CurrentResolutionIndex();

        public static void BeginEdit()
        {
            ResolutionList.Clear();
            PendingMode = CurrentModeIndex();
            PendingResolution = CurrentResolutionIndex();
        }

        public static void StepMode(int delta) => PendingMode = Wrap(PendingMode + delta, Modes.Length);

        public static void StepResolution(int delta) => PendingResolution = Wrap(PendingResolution + delta, Resolutions.Count);

        public static void Apply()
        {
            var r = Resolutions[PendingResolution];
            var mode = Modes[PendingMode];
            Screen.SetResolution(r.width, r.height, mode, r.refreshRateRatio);
            PlayerPrefs.SetInt(ModeKey, PendingMode);
            PlayerPrefs.SetInt(WidthKey, r.width);
            PlayerPrefs.SetInt(HeightKey, r.height);
            PlayerPrefs.Save();
        }

        /// <summary>Az Editorban nem nyúl a Game nézethez.</summary>
        public static void ApplySaved()
        {
            if (Application.isEditor) return;
            if (!PlayerPrefs.HasKey(ModeKey))
            {
                // Régi (bool) teljes képernyő beállítás átvétele.
                if (PlayerPrefs.HasKey(LegacyFullscreenKey) && PlayerPrefs.GetInt(LegacyFullscreenKey) == 0)
                    Screen.fullScreenMode = FullScreenMode.Windowed;
                return;
            }
            var mode = Modes[Mathf.Clamp(PlayerPrefs.GetInt(ModeKey), 0, Modes.Length - 1)];
            var width = PlayerPrefs.GetInt(WidthKey, Screen.currentResolution.width);
            var height = PlayerPrefs.GetInt(HeightKey, Screen.currentResolution.height);
            Screen.SetResolution(width, height, mode);
        }

        private static int CurrentModeIndex()
        {
            var index = System.Array.IndexOf(Modes, Screen.fullScreenMode);
            // A MaximizedWindow (macOS) és ismeretlen módok a keret nélküli ablakhoz állnak legközelebb.
            return index < 0 ? 0 : index;
        }

        private static int CurrentResolutionIndex()
        {
            var list = Resolutions;
            for (int i = 0; i < list.Count; i++)
                if (list[i].width == Screen.width && list[i].height == Screen.height) return i;
            return 0;
        }

        // Egyedi méretek csökkenő sorrendben; az aktuális ablakméret mindig szerepel.
        private static void BuildResolutions()
        {
            var byKey = new Dictionary<long, Resolution>();
            foreach (var r in Screen.resolutions)
            {
                if (r.width < 800 || r.height < 600) continue;
                var key = ((long)r.width << 32) | (uint)r.height;
                if (!byKey.TryGetValue(key, out var existing) || r.refreshRateRatio.value > existing.refreshRateRatio.value)
                    byKey[key] = r;
            }
            var current = new Resolution { width = Screen.width, height = Screen.height,
                refreshRateRatio = Screen.currentResolution.refreshRateRatio };
            var currentKey = ((long)current.width << 32) | (uint)current.height;
            if (!byKey.ContainsKey(currentKey)) byKey[currentKey] = current;

            ResolutionList.AddRange(byKey.Values);
            ResolutionList.Sort((a, b) => a.width != b.width ? b.width.CompareTo(a.width) : b.height.CompareTo(a.height));
        }

        private static int Wrap(int value, int count) => count <= 0 ? 0 : ((value % count) + count) % count;
    }
}
