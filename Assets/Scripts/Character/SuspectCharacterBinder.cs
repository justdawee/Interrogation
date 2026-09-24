using Interrogation.Fsm;
using Interrogation.Game;
using Interrogation.Llm;
using UnityEngine;

namespace Interrogation.Character
{
    /// <summary>
    /// Híd a Game/Body események és a karakter vizuális rétege között: frissíti a
    /// <see cref="SuspectMood"/>-ot és kimondatja a választ. Csak olvas a játékállapotból.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class SuspectCharacterBinder : MonoBehaviour
    {
        [SerializeField] private InterrogationManager manager;
        [SerializeField] private EmotionController emotion;
        [SerializeField] private SuspectMood mood;
        [SerializeField] private SuspectLipSync lipSync;

        [Header("Előnézet (fejlesztéshez, Play módban)")]
        [Tooltip("Bekapcsolva az alábbi értékek felülírják a játékból érkező állapotot.")]
        [SerializeField] private bool previewOverride;
        [SerializeField] private SuspectEmotion previewEmotion;
        [Range(0f, 1f)] [SerializeField] private float previewStress;
        [SerializeField] private bool previewThinking;
        [TextArea] [SerializeField] private string previewLine = "Otthon voltam. Egyedül. Ezt már elmondtam a kollégáinak, felügyelő.";

        private void Awake()
        {
            if (mood == null) mood = GetComponent<SuspectMood>();
            if (lipSync == null) lipSync = GetComponent<SuspectLipSync>();
        }

        private void OnEnable()
        {
            if (manager != null)
            {
                manager.ThinkingStarted += OnThinkingStarted;
                manager.TurnCompleted += OnTurnCompleted;
                manager.StressChanged += OnStressChanged;
                manager.GameEnded += OnGameEnded;
                manager.ScriptedLineSpoken += OnScriptedLine;
            }
            if (emotion != null) emotion.EmotionChanged += OnEmotionChanged;
        }

        private void OnDisable()
        {
            if (manager != null)
            {
                manager.ThinkingStarted -= OnThinkingStarted;
                manager.TurnCompleted -= OnTurnCompleted;
                manager.StressChanged -= OnStressChanged;
                manager.GameEnded -= OnGameEnded;
                manager.ScriptedLineSpoken -= OnScriptedLine;
            }
            if (emotion != null) emotion.EmotionChanged -= OnEmotionChanged;
        }

        private void Update()
        {
            if (!previewOverride) return;
            mood.SetEmotion(previewEmotion);
            mood.SetStress01(previewStress);
            mood.SetThinking(previewThinking);
        }

        public bool PreviewActive => previewOverride;

        /// <summary>Az előnézeti állapot felülírja a játékból érkezőt.</summary>
        public void SetPreview(SuspectEmotion emotion, float stress, bool thinking)
        {
            previewOverride = true;
            previewEmotion = emotion;
            previewStress = Mathf.Clamp01(stress);
            previewThinking = thinking;
        }

        public void ClearPreview()
        {
            previewOverride = false;
            mood.SetThinking(false);
        }

        [ContextMenu("Előnézet: mondat kimondása")]
        public void PreviewSpeak() => lipSync?.Speak(previewLine);

        private void OnThinkingStarted() => mood.SetThinking(true);

        [ContextMenu("Előnézet: rajtakapva-reakció")]
        public void PreviewCaught() => mood.RequestReaction(SuspectReaction.Caught);

        private void OnTurnCompleted(TurnResult turn)
        {
            mood.SetThinking(false);
            if (turn.Reply == null) return;
            if (turn.Reply.Contradiction.Detected) mood.RequestReaction(SuspectReaction.Caught);
            // Nyomás-győzelemnél a kör válasza nem hangzik el: a gyanúsított hallgat, majd vall.
            if (!turn.IsSupersededByConfession) lipSync?.Speak(turn.Reply.Dialogue);
        }

        private void OnScriptedLine(string line, SuspectEmotion value)
        {
            mood.SetEmotion(value);
            lipSync?.Speak(line);
        }

        private void OnStressChanged(int current, int delta, int threshold) =>
            mood.SetStress01(threshold > 0 ? (float)current / threshold : 0f);

        private void OnEmotionChanged(SuspectEmotion value) => mood.SetEmotion(value);

        private void OnGameEnded(InterrogationOutcome outcome)
        {
            mood.SetThinking(false);
            if (outcome == InterrogationOutcome.Confessed) mood.SetEmotion(SuspectEmotion.Breaking);
            else if (outcome == InterrogationOutcome.LawyeredUp)
            {
                mood.SetEmotion(SuspectEmotion.Defensive);
                mood.RequestReaction(SuspectReaction.SmugLaugh);
            }
        }
    }
}
