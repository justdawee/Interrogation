using System;
using System.Threading.Tasks;
using Interrogation.Llm;
using Interrogation.Memory;
using UnityEngine;

namespace Interrogation.Fsm
{
    /// <summary>
    /// A „Body" vezérlője: egy kihallgatási kör végrehajtása
    /// (Listening → Thinking async LLM-hívással → Responding).
    /// A stresszről, a memóriáról és a vég-feltételekről a Game réteg dönt, majd a
    /// <see cref="Resolve"/> hívással zárja a kört, így a Body sosem dönt mechanikáról.
    /// </summary>
    public sealed class SuspectController : MonoBehaviour
    {
        [Header("Eset & LLM")]
        [Tooltip("Az eset adatcsomagja (pl. Assets/Data/case_01.asset).")]
        public CaseData caseData;

        public LlmSettings settings = new LlmSettings();

        [Header("Hivatkozások")]
        [Tooltip("A hangulat-vezérlő. Ha üres, ugyanezen a GameObjecten keresi.")]
        [SerializeField] private EmotionController emotion;

        public SuspectStateMachine Fsm { get; private set; }
        public ConversationMemory Memory { get; private set; }
        public bool IsReady { get; private set; }

        private KnowledgeGraph _graph;
        private LlmClient _client;

        public event Action<SuspectState, SuspectState> StateChanged;
        public event Action ThinkingStarted;
        public event Action<SuspectReply, LlmResult> ReplyReady;

        private void Awake()
        {
            if (caseData != null && !IsReady) Initialize(caseData, settings);
        }

        /// <summary>Külön metódus, hogy Edit módban és a Game rétegből is hívható legyen.</summary>
        public void Initialize(CaseData data, LlmSettings llmSettings)
        {
            caseData = data != null ? data : throw new ArgumentNullException(nameof(data));
            settings = llmSettings ?? new LlmSettings();

            if (emotion == null) emotion = GetComponent<EmotionController>();

            Fsm = new SuspectStateMachine();
            Fsm.StateChanged += (from, to) => StateChanged?.Invoke(from, to);

            Memory = new ConversationMemory();
            _graph = caseData.BuildKnowledgeGraph();
            _client = new LlmClient(settings);
            IsReady = true;
        }

        public void Begin()
        {
            EnsureReady();
            if (Fsm.Current == SuspectState.Idle)
                Fsm.Transition(SuspectState.Listening);
        }

        /// <summary>
        /// A kör végén az állapot <see cref="SuspectState.Responding"/>; a hívó alkalmazza a mechanikát,
        /// majd <see cref="Resolve"/>-val zárja a kört. Hiba esetén is érvényes (fallback) eredményt ad.
        /// </summary>
        public async Task<LlmResult> AskAsync(string playerQuestion, int currentStress, ContradictionPair evidence = null)
        {
            EnsureReady();
            if (Fsm.Current != SuspectState.Listening)
                throw new InvalidOperationException(
                    $"AskAsync csak Listening állapotban hívható (most: {Fsm.Current}).");

            Fsm.Transition(SuspectState.Thinking);
            ThinkingStarted?.Invoke();

            var ctx = ContextBuilder.Build(caseData, _graph, Memory, playerQuestion, currentStress, evidence);

            var result = await _client.AskAsync(ctx.SystemPrompt, ctx.UserMessage);
            var reply = result.Reply;

            emotion?.Apply(reply.Emotion);

            Fsm.Transition(SuspectState.Responding);
            ReplyReady?.Invoke(reply, result);

            return result;
        }

        /// <summary>
        /// A vallomás-monológ a Game réteg győzelmi döntése után. Csak Confessed állapotban hívható,
        /// és nem vált állapotot: a modell itt már csak megfogalmazza a döntést.
        /// </summary>
        public async Task<LlmResult> ConfessAsync()
        {
            EnsureReady();
            if (Fsm.Current != SuspectState.Confessed)
                throw new InvalidOperationException($"ConfessAsync csak Confessed állapotban hívható (most: {Fsm.Current}).");

            var ctx = ConfessionPrompt.Build(caseData, Memory);
            var result = await _client.AskAsync(ctx.SystemPrompt, ctx.UserMessage);
            emotion?.Apply(SuspectEmotion.Breaking);
            return result;
        }

        /// <summary>Responding-ból, vagy korai vég-állapotnál (pl. feladás) Listening-ből hívható.</summary>
        public void Resolve(SuspectState next)
        {
            EnsureReady();
            Fsm.Transition(next);
        }

        private void EnsureReady()
        {
            if (!IsReady)
                throw new InvalidOperationException(
                    "A SuspectController nincs inicializálva. Hívd az Initialize()-t (vagy adj caseData-t).");
        }
    }
}
