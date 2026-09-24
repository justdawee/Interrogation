using Interrogation.Llm;
using UnityEngine;

namespace Interrogation.Character
{
    /// <summary>
    /// A <see cref="SuspectMood"/> alapján vezérli az Animatort: érzelem-paraméter, reakció-triggerek
    /// és a beszéd-réteg simán úsztatott súlya.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class SuspectAnimationDriver : MonoBehaviour
    {
        private static readonly int EmotionParam = Animator.StringToHash("Emotion");
        private static readonly int StressParam = Animator.StringToHash("Stress");
        private static readonly int CaughtTrigger = Animator.StringToHash("Caught");
        private static readonly int OutburstTrigger = Animator.StringToHash("Outburst");
        private static readonly int LaughTrigger = Animator.StringToHash("SmugLaugh");

        [SerializeField] private SuspectMood mood;
        [SerializeField] private SuspectLipSync lipSync;

        [Header("Beszéd-réteg")]
        [SerializeField] private string talkingLayerName = "Talking";
        [Tooltip("A beszéd-réteg be/ki úszásának ideje (mp).")]
        [SerializeField] private float talkingFade = 0.45f;
        [Range(0f, 1f)] [SerializeField] private float talkingWeight = 0.85f;

        private Animator _animator;
        private int _talkingLayer = -1;
        private float _talking;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (mood == null) mood = GetComponent<SuspectMood>();
            if (lipSync == null) lipSync = GetComponent<SuspectLipSync>();
            _talkingLayer = _animator.GetLayerIndex(talkingLayerName);
        }

        private void OnEnable()
        {
            if (mood == null) return;
            mood.EmotionChanged += OnEmotionChanged;
            mood.ReactionRequested += OnReaction;
        }

        private void OnDisable()
        {
            if (mood == null) return;
            mood.EmotionChanged -= OnEmotionChanged;
            mood.ReactionRequested -= OnReaction;
        }

        private void Update()
        {
            if (mood == null || _animator.runtimeAnimatorController == null) return;
            _animator.SetInteger(EmotionParam, (int)mood.Emotion);
            _animator.SetFloat(StressParam, mood.Stress01, 0.5f, Time.deltaTime);

            if (_talkingLayer < 0) return;
            var speaking = lipSync != null && lipSync.IsSpeaking;
            var goal = speaking ? talkingWeight * TalkScale(mood.Emotion) : 0f;
            _talking = Mathf.MoveTowards(_talking, goal, Time.deltaTime / Mathf.Max(0.05f, talkingFade));
            _animator.SetLayerWeight(_talkingLayer, Mathf.SmoothStep(0f, 1f, _talking));
        }

        // Összeomláskor alig gesztikulál, ellenségesen erősebben.
        private static float TalkScale(SuspectEmotion emotion) =>
            emotion == SuspectEmotion.Breaking ? 0.3f : emotion == SuspectEmotion.Hostile ? 1f : 0.85f;

        private void OnEmotionChanged(SuspectEmotion emotion)
        {
            if (emotion == SuspectEmotion.Hostile) _animator.SetTrigger(OutburstTrigger);
        }

        private void OnReaction(SuspectReaction reaction)
        {
            switch (reaction)
            {
                case SuspectReaction.Caught: _animator.SetTrigger(CaughtTrigger); break;
                case SuspectReaction.Outburst: _animator.SetTrigger(OutburstTrigger); break;
                case SuspectReaction.SmugLaugh: _animator.SetTrigger(LaughTrigger); break;
            }
        }
    }
}
