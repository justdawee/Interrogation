using System;
using Interrogation.Llm;
using UnityEngine;

namespace Interrogation.Audio
{
    /// <summary>
    /// Egy hang-jelzés egy vagy több változattal. Véletlenszerűen választ, de ugyanazt kétszer
    /// egymás után nem, így az ismétlődő hangok nem hatnak gépiesnek.
    /// </summary>
    [Serializable]
    public sealed class SoundCue
    {
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0f, 0.3f)] public float pitchJitter = 0.04f;

        [NonSerialized] private int _last = -1;

        public bool IsEmpty => clips == null || clips.Length == 0;

        public AudioClip Pick()
        {
            if (IsEmpty) return null;
            if (clips.Length == 1) return clips[0];
            var i = UnityEngine.Random.Range(0, clips.Length - 1);
            if (i >= _last) i++;
            _last = i;
            return clips[i];
        }

        public float PickPitch() => 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
    }

    /// <summary>
    /// A játék összes hangja. A fájlok típusonként azonos hangosságúak, ezért hangonkénti
    /// korrekció nincs: a hangerőt a játékos hangsáv-beállításai adják.
    /// </summary>
    [CreateAssetMenu(menuName = "Interrogation/Audio Library", fileName = "AudioLibrary")]
    public sealed class AudioLibrary : ScriptableObject
    {
        [Header("Zene")]
        public AudioClip musicMenu;
        public AudioClip musicInterrogation;
        public AudioClip musicStress;
        public AudioClip musicConfession;
        public AudioClip musicFail;

        [Header("Alapzaj (loop)")]
        public AudioClip rainLoop;
        public AudioClip roomToneLoop;
        public AudioClip lampBuzzLoop;
        public AudioClip heartbeatLoop;
        public AudioClip tensionDroneLoop;
        public AudioClip tapeHissLoop;
        public SoundCue foghorn = new SoundCue();

        [Header("Hanghatások")]
        public SoundCue tapeRec = new SoundCue();
        public SoundCue tapeStop = new SoundCue();
        public SoundCue paper = new SoundCue();
        public SoundCue folderOpen = new SoundCue();
        public SoundCue folderClose = new SoundCue();
        public SoundCue stamp = new SoundCue();
        public SoundCue typewriterKey = new SoundCue();
        public SoundCue typewriterBell = new SoundCue();
        public SoundCue chairCreak = new SoundCue();
        public SoundCue tableSlam = new SoundCue();
        public SoundCue stingContradiction = new SoundCue();
        public SoundCue stingReveal = new SoundCue();

        [Header("A gyanúsított hangja")]
        public SoundCue voiceCalm = new SoundCue();
        public SoundCue voiceNervous = new SoundCue();
        public SoundCue voiceDefensive = new SoundCue();
        public SoundCue voiceHostile = new SoundCue();
        public SoundCue voiceBreaking = new SoundCue();
        public AudioClip mumbleLoop;

        [Header("Felület")]
        public SoundCue uiHover = new SoundCue();
        public SoundCue uiClick = new SoundCue();
        public SoundCue uiBack = new SoundCue();
        public SoundCue uiSend = new SoundCue();
        public SoundCue uiOpen = new SoundCue();
        public SoundCue uiClose = new SoundCue();
        public SoundCue uiPause = new SoundCue();
        public SoundCue uiError = new SoundCue();

        public SoundCue Voice(SuspectEmotion emotion)
        {
            switch (emotion)
            {
                case SuspectEmotion.Nervous: return voiceNervous;
                case SuspectEmotion.Defensive: return voiceDefensive;
                case SuspectEmotion.Hostile: return voiceHostile;
                case SuspectEmotion.Breaking: return voiceBreaking;
                default: return voiceCalm;
            }
        }
    }
}
