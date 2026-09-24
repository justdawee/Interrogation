using System;
using Interrogation.Llm;
using UnityEngine;

namespace Interrogation.Character
{
    /// <summary>
    /// A gyanúsított pillanatnyi állapota a vizuális rétegek számára. Csak a
    /// <see cref="SuspectCharacterBinder"/> írja, a többi komponens csak olvassa.
    /// </summary>
    public sealed class SuspectMood : MonoBehaviour
    {
        public SuspectEmotion Emotion { get; private set; } = SuspectEmotion.Calm;

        public float Stress01 { get; private set; }

        public bool IsThinking { get; private set; }

        public event Action<SuspectEmotion> EmotionChanged;

        public event Action<SuspectReaction> ReactionRequested;

        public void RequestReaction(SuspectReaction reaction) => ReactionRequested?.Invoke(reaction);

        public void SetEmotion(SuspectEmotion emotion)
        {
            if (emotion == Emotion) return;
            Emotion = emotion;
            EmotionChanged?.Invoke(emotion);
        }

        public void SetStress01(float value) => Stress01 = Mathf.Clamp01(value);

        public void SetThinking(bool value) => IsThinking = value;
    }
}
