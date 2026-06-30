using System;
using System.Threading.Tasks;
using Interrogation.Fsm;
using Interrogation.Llm;
using Interrogation.Memory;
using UnityEngine;

namespace Interrogation.Game
{
    /// <summary>
    /// A kihallgatás-menet vezérlője. Egy kör: a Body végrehajtja (Thinking → Responding), az őr
    /// ellenőrzi az LLM állításait, majd a manager alkalmazza a stresszt, bővíti a memóriát,
    /// kiértékeli a vég-feltételt és lezárja a kört.
    /// </summary>
    public sealed class InterrogationManager : MonoBehaviour
    {
        [Header("Eset & LLM")]
        public CaseData caseData;
        public LlmSettings settings = new LlmSettings();

        [Header("Body")]
        [Tooltip("A gyanúsított vezérlője. Ha üres, ugyanezen a GameObjecten keresi.")]
        [SerializeField] private SuspectController suspect;

        [Header("Indulás")]
        [Tooltip("Ha igaz, Start-kor automatikusan inicializál és elindítja a menetet.")]
        public bool autoStart = true;

        public StressMeter Stress { get; private set; }
        public WinLoseController WinLose { get; private set; }
        public bool IsReady { get; private set; }
        public bool IsOver { get; private set; }
        public ConversationMemory Memory => suspect != null ? suspect.Memory : null;
        public TurnResult? LastTurn { get; private set; }

        /// <summary>Nyomás-győzelem, de az utolsó válasz nem vallomás, ezért monológ kell.</summary>
        public bool NeedsConfessionMonologue =>
            IsOver && LastTurn is TurnResult last && last.IsSupersededByConfession;

        private ReplyGuard _guard;

        public event Action ThinkingStarted;
        public event Action<TurnResult> TurnCompleted;
        public event Action<int, int, int> StressChanged;     // (current, delta, threshold)
        public event Action<InterrogationOutcome> GameEnded;
        public event Action<string, SuspectEmotion> ScriptedLineSpoken;

        private void Start()
        {
            if (autoStart && caseData != null) StartMatch();
        }

        public void StartMatch()
        {
            if (caseData == null) throw new InvalidOperationException("Nincs CaseData beállítva.");
            if (suspect == null) suspect = GetComponent<SuspectController>();
            if (suspect == null) throw new InvalidOperationException("Nincs SuspectController.");

            if (!suspect.IsReady) suspect.Initialize(caseData, settings);
            suspect.ThinkingStarted -= RelayThinkingStarted; // dupla feliratkozás ellen
            suspect.ThinkingStarted += RelayThinkingStarted;

            Stress = new StressMeter(caseData.stressThreshold);
            Stress.Changed += (cur, delta, thr) => StressChanged?.Invoke(cur, delta, thr);
            WinLose = new WinLoseController(caseData);
            _guard = new ReplyGuard(caseData);

            IsOver = false;
            LastTurn = null;
            IsReady = true;
            suspect.Begin();
        }

        /// <summary>Előre megírt mondat LLM-hívás és állapotváltozás nélkül.</summary>
        public void SpeakScripted(string line, SuspectEmotion emotion = SuspectEmotion.Calm)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            ScriptedLineSpoken?.Invoke(line.Trim(), emotion);
        }

        public async Task<TurnResult> SubmitAsync(string playerQuestion, string evidenceId = null)
        {
            EnsureReady();
            if (IsOver)
                throw new InvalidOperationException("A menet már véget ért.");

            var question = playerQuestion?.Trim() ?? string.Empty;
            var evidence = FindEvidence(evidenceId);

            var result = await suspect.AskAsync(question, Stress.Current, evidence);

            // Az őr a kör előtti memóriával dolgozik.
            var verdict = _guard.Review(result.Reply, question, evidence, suspect.Memory);
            var reply = verdict.Reply;

            // A mérőre a szorzott érték kerül, a türelem-szabály a szorzatlan pontokat nézi.
            var points = ComputeStressDelta(reply, caseData);
            var delta = ScaleStress(points, caseData);
            Stress.Apply(delta);

            suspect.Memory.Append(ContextBuilder.ComposeUserMessage(question, evidence), reply, Stress.Current,
                verdict.ExposedFactId);

            var outcome = WinLose.Evaluate(
                Stress,
                suspect.Memory.RevealedFactIds,
                suspect.Memory.Round,
                caseData.roundLimit,
                reply,
                points,
                evidencePresented: evidence != null);

            suspect.Resolve(outcome.ToState());

            var turn = new TurnResult(reply, result, verdict, outcome, suspect.Memory.Round, Stress.Current, delta,
                points, question, evidence != null ? evidence.factId : null);
            LastTurn = turn;
            TurnCompleted?.Invoke(turn);

            if (outcome.IsTerminal())
            {
                IsOver = true;
                GameEnded?.Invoke(outcome);
            }

            return turn;
        }

        /// <summary>Csak függő LLM-kör nélkül, különben a késő válasz a lezárás után írna az állapotba.</summary>
        public bool CanAbandon =>
            IsReady && !IsOver && suspect != null && suspect.Fsm != null && suspect.Fsm.Current == SuspectState.Listening;

        public void Abandon()
        {
            if (!CanAbandon) throw new InvalidOperationException("A menet most nem adható fel.");
            suspect.Resolve(InterrogationOutcome.Abandoned.ToState());
            IsOver = true;
            GameEnded?.Invoke(InterrogationOutcome.Abandoned);
        }

        public int PatienceLost => WinLose != null ? WinLoseController.PatienceMax - WinLose.Patience : 0;

        /// <summary>
        /// A modell megfogalmazza a monológot; érvénytelen válasznál a CaseData tartalék-vallomása lép be,
        /// így a felirat sosem mond ellent a már eldöntött kimenetnek. A mondatot a hívó mondatja ki,
        /// hogy a befejezés-jelenet időzíthesse.
        /// </summary>
        public async Task<(string Line, LlmResult Llm)> RequestConfessionAsync()
        {
            if (!NeedsConfessionMonologue) throw new InvalidOperationException("Nincs szükség vallomás-monológra.");
            var result = await suspect.ConfessAsync();
            return (SelectConfessionLine(result, caseData), result);
        }

        public static string SelectConfessionLine(LlmResult result, CaseData caseData)
        {
            var reply = result?.Reply;
            var valid = result != null && result.Success && reply != null && reply.Confession
                        && !string.IsNullOrWhiteSpace(reply.Dialogue);
            return valid ? reply.Dialogue.Trim() : caseData.confessionLine;
        }

        /// <summary>Az LLM stress_delta-ja, ellentmondásnál a CaseData bónuszával.</summary>
        public static int ComputeStressDelta(SuspectReply reply, CaseData caseData)
        {
            if (reply == null) throw new ArgumentNullException(nameof(reply));
            var delta = reply.StressDelta;
            if (reply.Contradiction.Detected && caseData != null)
                delta += caseData.contradictionStressBonus;
            return delta;
        }

        public static int ScaleStress(int points, CaseData caseData) =>
            points * (caseData != null ? Math.Max(1, caseData.stressMultiplier) : 1);

        public ContradictionPair FindEvidence(string evidenceId)
        {
            if (string.IsNullOrWhiteSpace(evidenceId) || caseData == null || caseData.contradictions == null)
                return null;
            foreach (var c in caseData.contradictions)
                if (c != null && string.Equals(c.factId, evidenceId, StringComparison.OrdinalIgnoreCase))
                    return c;
            return null;
        }

        private void RelayThinkingStarted() => ThinkingStarted?.Invoke();

        private void EnsureReady()
        {
            if (!IsReady)
                throw new InvalidOperationException("Az InterrogationManager nincs inicializálva. Hívd a StartMatch()-et.");
        }
    }
}
