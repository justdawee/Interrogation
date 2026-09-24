using System;
using System.Collections;
using System.Collections.Generic;
using Interrogation.Game;
using Interrogation.Memory;
using UnityEngine;
using UnityEngine.UI;

namespace Interrogation.UI
{
    /// <summary>
    /// A kihallgatás mozi-keretes HUD-ja: a Game réteget köti a felirathoz, a kérdés-sorhoz, a poligráfhoz,
    /// a szalag- és türelem-jelzőhöz, a toast-réteghez és az aktához. Csak olvas a játékállapotból.
    /// </summary>
    public sealed class InterrogationHUD : MonoBehaviour
    {
        [SerializeField] private InterrogationManager manager;
        [SerializeField] private SubtitleView subtitle;
        [SerializeField] private QuestionBar question;
        [SerializeField] private PolygraphGraph polygraph;
        [SerializeField] private TapeStatusView tapeStatus;
        [SerializeField] private ToastLayer toasts;
        [SerializeField] private CaseFilePanel caseFile;
        [SerializeField] private Button caseFileButton;

        [Tooltip("Késleltetés a vég-állapot után, mielőtt az eredmény-jelenetre váltunk (mp).")]
        [SerializeField] private float resultDelaySeconds = 3f;

        [Tooltip("Igaz: a játékfolyamot a GameDirector vezérli (menet-indítás, bevitel engedése, befejezés). " +
                 "Hamis: a HUD maga indítja a menetet és vált az eredmény-jelenetre (régi, önálló mód).")]
        [SerializeField] private bool externalFlow;

        [Tooltip("A detektív-kamera körülnézése — interakció alatt a HUD visszaállíttatja az alap-pózba.")]
        [SerializeField] private Cameras.CameraLookAround lookAround;

        private readonly HashSet<string> _announcedFacts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private CaseData Case => manager != null ? manager.caseData : null;
        private bool _inputBlocked;
        private int _unblockedFrame = -1;
        private Canvas _canvas;

        private void Start()
        {
            if (manager == null) { Debug.LogError("[HUD] Hiányzik az InterrogationManager."); return; }

            // Külső folyamnál a GameDirector indítja a menetet a bevezető után.
            if (!externalFlow && !manager.IsReady) manager.StartMatch();

            var limit = Case != null ? Case.roundLimit : 0;
            tapeStatus?.SetTape(0, limit);
            tapeStatus?.SetPatience(WinLoseController.PatienceMax, WinLoseController.PatienceMax, immediate: true);
            polygraph?.SetStress(0f);
            subtitle?.Clear();

            if (caseFile != null)
            {
                caseFile.Populate(Case);
                caseFile.Transcript?.Configure(Case != null ? Case.suspectName : null);
                caseFile.EvidenceChosen += OnEvidenceChosen;
                caseFile.OpenChanged += OnCaseFileOpenChanged;
            }
            if (caseFileButton != null) caseFileButton.onClick.AddListener(ToggleCaseFile);

            manager.ThinkingStarted += OnThinkingStarted;
            manager.StressChanged += OnStressChanged;
            manager.TurnCompleted += OnTurnCompleted;
            manager.GameEnded += OnGameEnded;
            manager.ScriptedLineSpoken += OnScriptedLine;

            if (question != null)
            {
                question.Submitted += OnSubmitted;
                question.SetInteractable(!externalFlow);
                if (!externalFlow) question.Focus();
            }
        }

        public void BeginInterrogation()
        {
            if (manager == null || manager.IsOver || question == null) return;
            question.SetInteractable(true);
            if (caseFile == null || !caseFile.IsOpen) question.Focus();
        }

        public event Action PauseRequested;

        /// <summary>Menük és összegzés alatt: nincs billentyű-reakció, az akta bezárul.</summary>
        public bool InputBlocked
        {
            get => _inputBlocked;
            set
            {
                if (_inputBlocked == value) return;
                _inputBlocked = value;
                if (!value) _unblockedFrame = Time.frameCount; // a feloldó billentyű ne hasson újra
                if (value) caseFile?.SetOpen(false);
                if (question == null) return;
                question.TypeToFocus = !value && (caseFile == null || !caseFile.IsOpen);
                if (value) question.Blur();
                else if (question.IsInteractable && question.TypeToFocus) question.Focus();
            }
        }

        public void SetVisible(bool visible)
        {
            if (_canvas == null && subtitle != null) _canvas = subtitle.GetComponentInParent<Canvas>();
            if (_canvas != null) _canvas.enabled = visible;
            if (caseFileButton != null) caseFileButton.gameObject.SetActive(visible);
        }

        public void ShowNotice(string message) => toasts?.ShowToast(message);

        public bool IsSubtitleTyping => subtitle != null && subtitle.IsTyping;

        /// <summary>A befejezés alatt a HUD bemenete tiltott, ezért a rendező innen ugorja át.</summary>
        public void SkipSubtitle() => subtitle?.Skip();

        private void OnDestroy()
        {
            if (manager != null)
            {
                manager.ThinkingStarted -= OnThinkingStarted;
                manager.StressChanged -= OnStressChanged;
                manager.TurnCompleted -= OnTurnCompleted;
                manager.GameEnded -= OnGameEnded;
                manager.ScriptedLineSpoken -= OnScriptedLine;
            }
            if (question != null) question.Submitted -= OnSubmitted;
            if (caseFile != null)
            {
                caseFile.EvidenceChosen -= OnEvidenceChosen;
                caseFile.OpenChanged -= OnCaseFileOpenChanged;
            }
            if (caseFileButton != null) caseFileButton.onClick.RemoveListener(ToggleCaseFile);
        }

        private void Update()
        {
            if (InputBlocked)
            {
                lookAround?.SetSuppressed(this, true);
                return;
            }
            if (Time.frameCount == _unblockedFrame) return;

            if (GameInput.CaseFile) ToggleCaseFile();
            else if (GameInput.Pause)
            {
                // Esc: előbb a nyitott aktát zárja, különben szünetet kér.
                if (caseFile != null && caseFile.IsOpen) caseFile.SetOpen(false);
                else PauseRequested?.Invoke();
            }

            if (caseFile != null && caseFile.IsOpen)
            {
                for (int i = 1; i <= 3; i++)
                    if (GameInput.Evidence(i)) { caseFile.ChooseByIndex(i); break; }
            }

            // Space csak akkor ugorja át a feliratot, ha nem a kérdés-sorba gépelünk.
            if (GameInput.SkipLine && (question == null || !question.IsFocused))
                subtitle?.Skip();

            lookAround?.SetSuppressed(this, IsInteracting());
        }

        // Interakció alatt a kamera az alap-pózba áll vissza.
        private bool IsInteracting()
        {
            if (caseFile != null && caseFile.IsOpen) return true;
            if (subtitle != null && subtitle.IsTyping) return true;
            if (question == null) return false;
            return question.HasText || question.AttachedEvidenceId != null || !question.IsInteractable;
        }

        private void ToggleCaseFile() => caseFile?.Toggle();

        private void OnCaseFileOpenChanged(bool open)
        {
            if (question == null) return;
            if (_inputBlocked) { question.TypeToFocus = false; return; }
            question.TypeToFocus = !open;
            if (open) question.Blur();
            else question.Focus();
        }

        private void OnEvidenceChosen(ContradictionPair evidence)
        {
            if (question == null || evidence == null) return;
            question.AttachEvidence(evidence.factId, CaseFileLabel(evidence));
        }

        private async void OnSubmitted(string text, string evidenceId)
        {
            if (manager == null || manager.IsOver) return;

            caseFile?.Transcript?.AddDetectiveLine(DetectiveLine(text, evidenceId));
            question?.SetInteractable(false);

            try
            {
                await manager.SubmitAsync(text, evidenceId);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                subtitle?.Clear();
                if (!manager.IsOver) question?.SetInteractable(true);
            }
        }

        private void OnThinkingStarted() => subtitle?.ShowThinking();

        private void OnScriptedLine(string line, Llm.SuspectEmotion emotion)
        {
            subtitle?.ShowLine(line, emotion);
            caseFile?.Transcript?.AddSuspectLine(line, emotion);
        }

        private void OnStressChanged(int current, int delta, int threshold)
        {
            if (polygraph == null) return;
            polygraph.SetStress(threshold > 0 ? (float)current / threshold : 0f);
            // A kitérés a küszöbhöz mért változással arányos.
            if (delta != 0 && threshold > 0) polygraph.Kick(Mathf.Abs(delta) / (threshold * 0.25f));
        }

        private void OnTurnCompleted(TurnResult turn)
        {
            var reply = turn.Reply;
            // Nyomás-győzelemnél a tagadó válasz helyett a vallomás-monológ szól.
            if (!turn.IsSupersededByConfession)
            {
                subtitle?.ShowLine(reply.Dialogue, reply.Emotion);
                caseFile?.Transcript?.AddSuspectLine(reply.Dialogue, reply.Emotion);
            }

            if (Case != null && manager.Memory != null) tapeStatus?.SetTape(manager.Memory.Round, Case.roundLimit);
            if (manager.WinLose != null && tapeStatus != null)
            {
                var patience = manager.WinLose.Patience;
                var change = tapeStatus.SetPatience(patience, WinLoseController.PatienceMax);
                if (change < 0 && !turn.Outcome.IsTerminal()) toasts?.ShowToast(PatienceWarning(patience));
            }

            if (reply.Contradiction.Detected)
            {
                toasts?.ShowStamp("ELLENTMONDÁS");
                polygraph?.Kick(1f);
            }
            AnnounceFact(reply.RevealedFactId);
            AnnounceFact(turn.Guard.ExposedFactId);
            if (manager.Memory != null) caseFile?.RefreshClues(manager.Memory.RevealedFactIds);

            if (!turn.Outcome.IsTerminal())
            {
                question?.SetInteractable(true);
                if (!_inputBlocked && (caseFile == null || !caseFile.IsOpen)) question?.Focus();
            }
        }

        private void AnnounceFact(string factId)
        {
            if (string.IsNullOrWhiteSpace(factId) || !_announcedFacts.Add(factId) || Case == null) return;
            var fact = Case.facts.Find(f => f != null && string.Equals(f.id, factId, StringComparison.OrdinalIgnoreCase));
            if (fact != null) toasts?.ShowClue(CaseFilePanel.ClueText(fact));
        }

        private void OnGameEnded(InterrogationOutcome outcome)
        {
            question?.SetInteractable(false);
            toasts?.ShowStamp(EndingStamp(outcome));
            if (externalFlow) return; // az összegzést a GameDirector intézi

            var title = Case != null ? Case.caseTitle : string.Empty;
            var suspect = Case != null ? Case.suspectName : string.Empty;
            GameSession.Record(outcome, title, suspect, manager.Memory != null ? manager.Memory.Round : 0, manager.Stress != null ? manager.Stress.Current : 0);

            StartCoroutine(LoadResultAfterDelay());
        }

        private IEnumerator LoadResultAfterDelay()
        {
            yield return new WaitForSeconds(resultDelaySeconds);
            GameFlow.LoadResult();
        }

        private string DetectiveLine(string text, string evidenceId)
        {
            var evidence = Case?.contradictions?.Find(c => c != null && string.Equals(c.factId, evidenceId, StringComparison.OrdinalIgnoreCase));
            if (evidence == null) return text;
            var tag = $"<color=#E0A867>[{CaseFileLabel(evidence)}]</color>";
            return string.IsNullOrEmpty(text) ? tag : $"{tag} {text}";
        }

        public static string EndingStamp(InterrogationOutcome outcome)
        {
            switch (outcome)
            {
                case InterrogationOutcome.Confessed: return "ÜGY LEZÁRVA";
                case InterrogationOutcome.LawyeredUp: return "ÜGYVÉD KÉRVE";
                case InterrogationOutcome.OutOfQuestions: return "A SZALAG LEJÁRT";
                case InterrogationOutcome.Abandoned: return "FELADVA";
                default: return string.Empty;
            }
        }

        private static string PatienceWarning(int patience) =>
            patience <= 1
                ? "Még egy alaptalan vád, és ügyvédet kér."
                : "Bizonyíték nélküli vád — a gyanúsított türelme fogy.";

        private static string CaseFileLabel(ContradictionPair e) =>
            string.IsNullOrEmpty(e.evidenceLabel) ? e.factId : char.ToUpperInvariant(e.evidenceLabel[0]) + e.evidenceLabel.Substring(1);
    }
}
