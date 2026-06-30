using System;
using Interrogation.Llm;
using UnityEngine;

namespace Interrogation.Fsm
{
    /// <summary>
    /// Az LLM <c>emotion</c> mezőjét a látható hangulathoz köti; váltáskor eseményt ad,
    /// és ha van Animator, a megfelelő paramétert is beállítja.
    /// </summary>
    public sealed class EmotionController : MonoBehaviour
    {
        [Header("Animator-kötés (opcionális, S7)")]
        [Tooltip("A gyanúsított Animatora. Ha üres, csak logikai a hangulat-váltás.")]
        [SerializeField] private Animator animator;

        [Tooltip("Az Animator egész paramétere, amire az emóció kódját írjuk (SuspectEmotion sorszáma).")]
        [SerializeField] private string animatorParameter = "Emotion";

        public SuspectEmotion Current { get; private set; } = SuspectEmotion.Calm;

        /// <summary>Csak tényleges változáskor sül el.</summary>
        public event Action<SuspectEmotion> EmotionChanged;

        public void Apply(SuspectEmotion emotion)
        {
            if (emotion == Current) return;
            Current = emotion;

            if (animator != null && !string.IsNullOrEmpty(animatorParameter))
                animator.SetInteger(animatorParameter, (int)emotion);

            EmotionChanged?.Invoke(emotion);
        }

        public void ResetEmotion() => Apply(SuspectEmotion.Calm);

        public void AttachAnimator(Animator value) => animator = value;
    }
}
