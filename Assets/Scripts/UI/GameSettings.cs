using System;
using UnityEngine;

namespace Interrogation.UI
{
    public enum SoundChannel
    {
        Master,
        MenuMusic,
        GameMusic,
        Sfx,
        Ambient,
        Voice
    }

    /// <summary>
    /// Játékos-beállítások: felirat-tempó és hangerő hangsávonként, <see cref="PlayerPrefs"/>-ben.
    /// A kijelző és a grafika az <c>Interrogation.Settings</c> névtérben van.
    /// </summary>
    public static class GameSettings
    {
        private const string SpeedKey = "Interrogation.SubtitleSpeed";
        private const string VolumeKey = "Interrogation.MasterVolume";

        public static readonly string[] SpeedLabels = { "Lassú", "Normál", "Gyors", "Azonnali" };
        private static readonly float[] SpeedFactors = { 0.7f, 1f, 1.6f, 4f };

        // Tized-lépésekben, a SoundChannel sorrendjében.
        private static readonly int[] DefaultSteps = { 8, 7, 6, 8, 6, 10 };

        private static int _speed = -1;
        private static readonly int[] Steps = new int[DefaultSteps.Length];

        public static event Action Changed;

        public static int SpeedIndex
        {
            get { Load(); return _speed; }
            set { _speed = Mathf.Clamp(value, 0, SpeedFactors.Length - 1); Save(SpeedKey, _speed); }
        }

        /// <summary>Az írógép és az ajakmozgás közös tempó-szorzója.</summary>
        public static float SubtitleSpeed => SpeedFactors[SpeedIndex];

        public static int GetVolumeStep(SoundChannel channel)
        {
            Load();
            return Steps[(int)channel];
        }

        public static void SetVolumeStep(SoundChannel channel, int step)
        {
            Load();
            var value = Mathf.Clamp(step, 0, 10);
            Steps[(int)channel] = value;
            if (channel == SoundChannel.Master) AudioListener.volume = value / 10f;
            Save(KeyFor(channel), value);
        }

        // A fő hangerőt az AudioListener kezeli, ezért itt nem szorzunk vele.
        public static float Volume(SoundChannel channel) =>
            channel == SoundChannel.Master ? 1f : GetVolumeStep(channel) / 10f;

        public static void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(SpeedKey);
            for (int i = 0; i < Steps.Length; i++) PlayerPrefs.DeleteKey(KeyFor((SoundChannel)i));
            _speed = -1;
            Load();
            AudioListener.volume = Steps[(int)SoundChannel.Master] / 10f;
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static string KeyFor(SoundChannel channel) =>
            channel == SoundChannel.Master ? VolumeKey : $"Interrogation.Volume.{channel}";

        private static void Load()
        {
            if (_speed >= 0) return;
            _speed = Mathf.Clamp(PlayerPrefs.GetInt(SpeedKey, 1), 0, SpeedFactors.Length - 1);
            for (int i = 0; i < Steps.Length; i++)
                Steps[i] = Mathf.Clamp(PlayerPrefs.GetInt(KeyFor((SoundChannel)i), DefaultSteps[i]), 0, 10);
        }

        private static void Save(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnStart()
        {
            _speed = -1;
            Load();
            AudioListener.volume = Steps[(int)SoundChannel.Master] / 10f;
        }
    }
}
