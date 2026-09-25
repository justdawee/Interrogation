using System;
using System.Collections;
using System.Threading.Tasks;
using Interrogation.Cameras;
using Interrogation.Game;
using Interrogation.Llm;
using Interrogation.Rendering;
using Interrogation.Telemetry;
using Interrogation.UI;
using UnityEngine;

namespace Interrogation.Flow
{
    /// <summary>
    /// A játékfolyam rendezője egyetlen jelenetben, töltőképernyő nélkül: főmenü → eligazítás
    /// (közben bemelegítő LLM-hívás) → kamera-út → REC → nyitómondat → kihallgatás → befejezés →
    /// „Lezárt akta". Csak a Game réteg API-ját hívja, játékállapotot nem ír.
    /// </summary>
    public sealed class GameDirector : MonoBehaviour
    {
        [Header("Hivatkozások")]
        [SerializeField] private InterrogationManager manager;
        [SerializeField] private InterrogationHUD hud;
        [SerializeField] private CameraLookAround lookAround;
        [Tooltip("Cinemachine kamera-rendező (M7). Ha nincs, a régi, kódból mozgatott kamera-út fut.")]
        [SerializeField] private CameraDirector cameraDirector;
        [SerializeField] private TapeRecorderController tape;
        [SerializeField] private CctvRecordingLight cctvLight;
        [SerializeField] private LatencyLogger telemetry;

        [Header("Menü és összegzés (M6)")]
        [SerializeField] private MainMenuView mainMenu;
        [SerializeField] private CaseClosedView caseClosed;
        [SerializeField] private PauseController pause;
        [SerializeField] private ScreenFader fader;
        [Tooltip("A főmenü kamerapozíciója (világtér) — a megfigyelő-ablak felőli sarok.")]
        [SerializeField] private Vector3 menuCameraPosition = new Vector3(-1.25f, 1.55f, -2.1f);
        [Tooltip("A főmenü kamerájának célpontja (világtér) — a gyanúsított.")]
        [SerializeField] private Vector3 menuLookAt = new Vector3(0.7f, 0.95f, -0.1f);
        [SerializeField] private float menuToSeatSeconds = 3.2f;
        [SerializeField] private float fadeSeconds = 0.6f;

        [Header("Bevezető")]
        [Tooltip("Újra esetén a kamera-út kezdőpontja a detektív-nézethez képest (a kamera lokális terében, m).")]
        [SerializeField] private Vector3 introOffset = new Vector3(-0.25f, 0.3f, -0.85f);
        [SerializeField] private float introSeconds = 3.5f;
        [Tooltip("Szünet a REC után, mielőtt a gyanúsított megszólal (mp).")]
        [SerializeField] private float recToLineSeconds = 0.8f;
        [Tooltip("Ennyit vár legfeljebb a bemelegítő hívásra a hurok megnyitása előtt (mp).")]
        [SerializeField] private float warmupWaitSeconds = 6f;

        [Header("Csúcspont")]
        [Tooltip("A stressz ekkora hányadánál (és a kulcs-ellentmondások megléténél) indul a csúcspont.")]
        [SerializeField] [Range(0.5f, 1f)] private float climaxStressFraction = 0.8f;

        [Header("Befejezés")]
        [Tooltip("Csend a befejező jelenet végén az összegzés előtt (mp) — Space-szel átugorható.")]
        [SerializeField] private float endingSeconds = 2.5f;
        [Tooltip("Az ügyvédes befejezésnél elhalványuló fények (opcionális).")]
        [SerializeField] private Light[] endingDimLights = Array.Empty<Light>();
        [SerializeField] [Range(0f, 1f)] private float endingDimTo = 0.35f;
        [Tooltip("Nyomás-győzelemnél ennyit vár legfeljebb a vallomás-monológra, utána a tartalék-vallomás jön (mp).")]
        [SerializeField] private float confessionWaitSeconds = 8f;
        [Tooltip("Csend az utolsó (tagadó) mondat és a megtört vallomás között (mp).")]
        [SerializeField] private float confessionPauseSeconds = 1.5f;

        private readonly GamePhaseMachine _phases = new GamePhaseMachine(GamePhase.Menu);
        private Task<LlmResult> _warmup;
        private Task<(string Line, LlmResult Llm)> _confession;
        private Light _cctv;
        private Transform _cam;
        private Vector3 _seatPos;
        private Quaternion _seatRot;
        private bool _leaving;

        public GamePhase Phase => _phases.Current;
        public event Action<GamePhase, GamePhase> PhaseChanged;

        public bool CanPause => _phases.IsPlayable && !_leaving && manager != null && !manager.IsOver;

        private void Awake()
        {
            if (manager == null) manager = FindAnyObjectByType<InterrogationManager>();
            // A menet indítását és a felvételt a rendező vezérli.
            if (manager != null) manager.autoStart = false;
            if (tape != null) tape.startRecordingOnPlay = false;
            _phases.Changed += (from, to) => PhaseChanged?.Invoke(from, to);
        }

        private void OnEnable()
        {
            if (manager != null)
            {
                manager.StressChanged += OnStressChanged;
                manager.GameEnded += OnGameEnded;
            }
            if (mainMenu != null)
            {
                mainMenu.NewCaseRequested += OnNewCase;
                mainMenu.BeginRequested += OnBegin;
                mainMenu.BackRequested += OnBackToCover;
            }
            if (caseClosed != null)
            {
                caseClosed.RetryRequested += OnRetry;
                caseClosed.MainMenuRequested += OnMainMenu;
            }
            if (pause != null) pause.MainMenuRequested += OnMainMenu;
        }

        private void OnDisable()
        {
            if (manager != null)
            {
                manager.StressChanged -= OnStressChanged;
                manager.GameEnded -= OnGameEnded;
            }
            if (mainMenu != null)
            {
                mainMenu.NewCaseRequested -= OnNewCase;
                mainMenu.BeginRequested -= OnBegin;
                mainMenu.BackRequested -= OnBackToCover;
            }
            if (caseClosed != null)
            {
                caseClosed.RetryRequested -= OnRetry;
                caseClosed.MainMenuRequested -= OnMainMenu;
            }
            if (pause != null) pause.MainMenuRequested -= OnMainMenu;
        }

        private void Start()
        {
            if (manager == null || manager.caseData == null)
            {
                Debug.LogError("[GameDirector] Hiányzik az InterrogationManager vagy a CaseData.", this);
                enabled = false;
                return;
            }
            SetRecordingLight(false);
            if (lookAround != null) lookAround.enabled = false;
            _cam = lookAround != null ? lookAround.transform : Camera.main.transform;
            _seatPos = _cam.position;
            _seatRot = _cam.rotation;

            hud?.SetVisible(false);
            if (hud != null) hud.InputBlocked = true;
            mainMenu?.Populate(manager.caseData);
            if (fader != null) StartCoroutine(fader.Fade(0f, fadeSeconds));

            var skipMenu = GameFlow.SkipMenuOnce || mainMenu == null;
            GameFlow.SkipMenuOnce = false;
            if (skipMenu)
            {
                _phases.Transition(GamePhase.Intro);
                StartWarmup();
                StartCoroutine(IntroRoutine(fromMenu: false));
            }
            else
            {
                if (cameraDirector == null)
                    _cam.SetPositionAndRotation(menuCameraPosition, Quaternion.LookRotation(menuLookAt - menuCameraPosition, Vector3.up));
                mainMenu.ShowCover();
            }
        }

        private void OnNewCase()
        {
            if (_phases.Current != GamePhase.Menu) return;
            _phases.Transition(GamePhase.Briefing);
            StartWarmup();
            mainMenu.ShowBriefing();
        }

        private void OnBackToCover()
        {
            if (_phases.Current != GamePhase.Briefing) return;
            _phases.Transition(GamePhase.Menu);
            mainMenu.ShowCover();
        }

        private void OnBegin()
        {
            if (_phases.Current != GamePhase.Briefing) return;
            _phases.Transition(GamePhase.Intro);
            mainMenu.Hide();
            StartCoroutine(IntroRoutine(fromMenu: true));
        }

        private void StartWarmup()
        {
            if (_warmup == null) _warmup = LlmWarmup.RunAsync(manager.settings);
        }

        private IEnumerator IntroRoutine(bool fromMenu)
        {
            if (fromMenu && cameraDirector != null) yield return AwaitCameraBlend();
            else if (fromMenu) yield return CameraMove.Glide(_cam, _seatPos, _seatRot, menuToSeatSeconds);
            else yield return CameraMove.IntroDolly(_cam, introOffset, introSeconds);
            hud?.SetVisible(true);

            tape?.StartRecording();
            SetRecordingLight(true);
            yield return new WaitForSeconds(recToLineSeconds);

            manager.SpeakScripted(manager.caseData.openingLine);
            yield return null;
            while (hud != null && hud.IsSubtitleTyping) yield return null;

            yield return AwaitWarmup();
            BeginInterrogation();
        }

        private IEnumerator AwaitCameraBlend()
        {
            yield return null;
            while (cameraDirector.IsTransitioning) yield return null;
        }

        private IEnumerator AwaitWarmup()
        {
            var deadline = Time.time + warmupWaitSeconds;
            while (_warmup != null && !_warmup.IsCompleted && Time.time < deadline) yield return null;
            if (_warmup == null || !_warmup.IsCompleted) yield break; // lassú hálózatnál nem tartjuk fel a játékost

            var result = _warmup.IsFaulted ? null : _warmup.Result;
            telemetry?.LogWarmup(result);
            if (result == null || !result.Success)
                hud?.ShowNotice(LlmWarmup.HasApiKey(manager.settings)
                    ? "Az LLM-szolgáltatás nem érhető el — a gyanúsított hallgatni fog."
                    : "Nincs API-kulcs (OPENAI_API_KEY) — a gyanúsított hallgatni fog.");
        }

        private void BeginInterrogation()
        {
            if (lookAround != null)
            {
                lookAround.CaptureBase();
                lookAround.enabled = true;
            }
            manager.StartMatch();
            _phases.Transition(GamePhase.Interrogation);
            if (hud != null) hud.InputBlocked = false;
            hud?.BeginInterrogation();
        }

        private void OnStressChanged(int current, int delta, int threshold)
        {
            if (_phases.Current != GamePhase.Interrogation || threshold <= 0 || manager.WinLose == null) return;
            var keyRevealed = manager.WinLose.CountKeyRevealed(manager.Memory.RevealedFactIds);
            if (current >= threshold * climaxStressFraction && keyRevealed >= manager.caseData.minKeyContradictionsToBreak)
                _phases.Transition(GamePhase.Climax);
        }

        private void OnGameEnded(InterrogationOutcome outcome)
        {
            if (!_phases.IsPlayable) return;
            _phases.Transition(GamePhase.Ending);
            if (hud != null) hud.InputBlocked = true;
            // A monológ-hívás azonnal indul, és a megtört csend alatt fut, így a várakozás a jelenet része.
            _confession = manager.NeedsConfessionMonologue ? manager.RequestConfessionAsync() : null;
            StartCoroutine(EndingRoutine(outcome));
        }

        private IEnumerator EndingRoutine(InterrogationOutcome outcome)
        {
            yield return AwaitLine();

            switch (outcome)
            {
                case InterrogationOutcome.OutOfQuestions:
                case InterrogationOutcome.Abandoned:
                    tape?.StopRecording();
                    break;
                case InterrogationOutcome.Confessed:
                    if (_confession != null) yield return ConfessionMonologue();
                    yield return SkippableWait(0.8f);
                    tape?.StopRecording();
                    break;
                case InterrogationOutcome.LawyeredUp:
                    yield return FadeLights(endingDimLights, endingDimTo, 1.5f);
                    tape?.StopRecording();
                    break;
            }
            SetRecordingLight(false);
            yield return SkippableWait(outcome == InterrogationOutcome.Abandoned ? endingSeconds * 0.4f : endingSeconds);
            yield return null; // az átugró lenyomás ne érje el az összegzés menüjét

            _phases.Transition(GamePhase.Summary);
            ShowSummary(outcome);
        }

        private void ShowSummary(InterrogationOutcome outcome)
        {
            var c = manager.caseData;
            var rounds = manager.Memory != null ? manager.Memory.Round : 0;
            var revealed = manager.Memory != null ? manager.Memory.RevealedFactIds : null;
            GameSession.Record(outcome, c.caseTitle, c.suspectName, rounds, manager.Stress != null ? manager.Stress.Current : 0);

            if (lookAround != null) lookAround.enabled = false;
            hud?.SetVisible(false);
            var report = CaseGrader.Grade(c, outcome, rounds, revealed, manager.PatienceLost);
            if (caseClosed != null) caseClosed.Show(c, outcome, rounds, revealed, report);
            else GameFlow.LoadResult(); // önálló mód, overlay nélkül
        }

        private void OnRetry() => StartCoroutine(Leave(skipMenu: true));
        private void OnMainMenu() => StartCoroutine(Leave(skipMenu: false));

        private IEnumerator Leave(bool skipMenu)
        {
            if (_leaving) yield break;
            _leaving = true;
            if (fader != null) yield return fader.Fade(1f, fadeSeconds);
            GameFlow.RestartInterrogation(skipMenu);
        }

        private IEnumerator ConfessionMonologue()
        {
            var deadline = Time.time + Mathf.Max(confessionWaitSeconds, confessionPauseSeconds);
            var pauseEnd = Time.time + confessionPauseSeconds;
            while (Time.time < pauseEnd || (!_confession.IsCompleted && Time.time < deadline)) yield return null;

            var completed = _confession.IsCompletedSuccessfully;
            var line = completed ? _confession.Result.Line : manager.caseData.confessionLine;
            if (completed)
                telemetry?.LogConfession(_confession.Result.Llm, line != manager.caseData.confessionLine);
            _confession = null;

            manager.SpeakScripted(line, SuspectEmotion.Breaking);
            yield return AwaitLine();
        }

        // A befejezés alatt a HUD bemenete tiltott, ezért a Space-átugrást itt kezeljük.
        private IEnumerator AwaitLine()
        {
            yield return null;
            while (hud != null && hud.IsSubtitleTyping)
            {
                if (GameInput.SkipLine) hud.SkipSubtitle();
                yield return null;
            }
        }

        private static IEnumerator SkippableWait(float seconds)
        {
            yield return null; // egy előző átugrás lenyomása ne hasson újra
            var end = Time.time + seconds;
            while (Time.time < end && !GameInput.SkipLine) yield return null;
        }

        private static IEnumerator FadeLights(Light[] lights, float factor, float seconds)
        {
            if (lights == null || lights.Length == 0) yield break;
            var from = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) from[i] = lights[i] != null ? lights[i].intensity : 0f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / seconds);
                for (int i = 0; i < lights.Length; i++)
                    if (lights[i] != null) lights[i].intensity = Mathf.Lerp(from[i], from[i] * factor, k);
                yield return null;
            }
        }

        private void SetRecordingLight(bool recording)
        {
            if (cctvLight == null) return;
            if (_cctv == null) _cctv = cctvLight.GetComponent<Light>();
            cctvLight.enabled = recording;
            if (!recording && _cctv != null) _cctv.intensity = 0f;
        }
    }
}
