using System.Collections;
using System.Collections.Generic;
using Interrogation.Character;
using Interrogation.Flow;
using Interrogation.Game;
using Interrogation.Llm;
using Interrogation.Rendering;
using Interrogation.UI;
using UnityEngine;

namespace Interrogation.Audio
{
    /// <summary>
    /// A játék hangképe a játékfolyam eseményeire kötve: fázis szerinti zene, a stresszel erősödő
    /// szívverés és zúgás, Thinking alatt lehalkított zene, magnó, pecsétek és kör-események.
    /// </summary>
    public sealed class GameAudioDirector : MonoBehaviour
    {
        [SerializeField] private AudioLibrary library;
        [SerializeField] private MusicPlayer music;
        [SerializeField] private SoundPlayer sfx;

        [Header("Hivatkozások")]
        [SerializeField] private GameDirector director;
        [SerializeField] private InterrogationManager manager;
        [SerializeField] private SuspectMood mood;
        [SerializeField] private TapeRecorderController tape;
        [SerializeField] private ToastLayer toasts;
        [Tooltip("A gyanúsított feje / törzse — innen szólnak a szék- és asztal-hangok.")]
        [SerializeField] private Transform suspectAnchor;

        [Header("Zene")]
        [Tooltip("A zene szorzója, amíg a gyanúsított gondolkodik (LLM-hívás).")]
        [SerializeField] [Range(0f, 1f)] private float thinkingDuck = 0.55f;
        [Tooltip("E stressz-szint (0..1) felett vált a zene a feszült témára (csúcspontnál mindig).")]
        [SerializeField] [Range(0f, 1f)] private float stressMusicOn = 0.75f;
        [SerializeField] [Range(0f, 1f)] private float stressMusicOff = 0.55f;

        [Header("Stressz-rétegek (ettől a szinttől erősödnek teljes hangerőig)")]
        [SerializeField] [Range(0f, 1f)] private float heartbeatFrom = 0.3f;
        [SerializeField] [Range(0f, 1f)] private float droneFrom = 0.45f;

        [Header("Alkalmi hangok")]
        [SerializeField] private Vector2 foghornInterval = new Vector2(45f, 90f);
        [SerializeField] [Range(0f, 1f)] private float creakOnThinkingChance = 0.35f;

        private readonly HashSet<string> _revealed = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        private LoopLayer _rain, _room, _lamp, _heart, _drone, _hiss;
        private InterrogationOutcome _outcome;
        private bool _stressTheme;
        private float _nextFoghorn;

        private GamePhase Phase => director != null ? director.Phase : GamePhase.Interrogation;
        private bool Playable => Phase == GamePhase.Interrogation || Phase == GamePhase.Climax;
        private Vector3 SuspectPos => suspectAnchor != null ? suspectAnchor.position : transform.position;

        private static float SfxVolume => GameSettings.Volume(SoundChannel.Sfx);
        private static float AmbientVolume => GameSettings.Volume(SoundChannel.Ambient);

        private void Awake()
        {
            if (library == null) { enabled = false; return; }
            _rain = new LoopLayer(gameObject, "Rain", library.rainLoop);
            _room = new LoopLayer(gameObject, "RoomTone", library.roomToneLoop);
            _lamp = new LoopLayer(gameObject, "LampBuzz", library.lampBuzzLoop);
            _heart = new LoopLayer(gameObject, "Heartbeat", library.heartbeatLoop, randomStart: false) { FadeSpeed = 0.25f };
            _drone = new LoopLayer(gameObject, "TensionDrone", library.tensionDroneLoop) { FadeSpeed = 0.15f };
            _hiss = new LoopLayer(gameObject, "TapeHiss", library.tapeHissLoop, spatialBlend: 0.6f) { FadeSpeed = 1.5f };
        }

        private void OnEnable()
        {
            if (director != null) director.PhaseChanged += OnPhaseChanged;
            if (tape != null) tape.RecordingChanged += OnRecordingChanged;
            if (mood != null) mood.EmotionChanged += OnEmotionChanged;
            if (toasts != null)
            {
                toasts.StampShown += OnStamp;
                toasts.ToastShown += OnToast;
            }
            if (manager == null) return;
            manager.ThinkingStarted += OnThinkingStarted;
            manager.TurnCompleted += OnTurnCompleted;
            manager.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            if (director != null) director.PhaseChanged -= OnPhaseChanged;
            if (tape != null) tape.RecordingChanged -= OnRecordingChanged;
            if (mood != null) mood.EmotionChanged -= OnEmotionChanged;
            if (toasts != null)
            {
                toasts.StampShown -= OnStamp;
                toasts.ToastShown -= OnToast;
            }
            if (manager == null) return;
            manager.ThinkingStarted -= OnThinkingStarted;
            manager.TurnCompleted -= OnTurnCompleted;
            manager.GameEnded -= OnGameEnded;
        }

        private void Start()
        {
            _rain.Snap(1f, AmbientVolume);
            _room.Snap(1f, AmbientVolume);
            _lamp.Snap(1f, AmbientVolume);
            OnPhaseChanged(Phase, Phase);
            _nextFoghorn = Time.time + 20f;
        }

        private void OnPhaseChanged(GamePhase from, GamePhase to)
        {
            switch (to)
            {
                case GamePhase.Menu:
                case GamePhase.Briefing:
                    music.Play(library.musicMenu, SoundChannel.MenuMusic, 1.5f);
                    break;
                case GamePhase.Intro:
                case GamePhase.Interrogation:
                    _stressTheme = false;
                    music.Play(library.musicInterrogation, SoundChannel.GameMusic, 4f);
                    break;
                case GamePhase.Climax:
                    SetStressTheme(true);
                    break;
                case GamePhase.Ending:
                    music.Stop(2.5f);
                    break;
                case GamePhase.Summary:
                    var confessed = _outcome == InterrogationOutcome.Confessed;
                    music.Play(confessed ? library.musicConfession : library.musicFail, SoundChannel.GameMusic, 1.5f);
                    StartCoroutine(SummaryStamp());
                    break;
            }
        }

        private IEnumerator SummaryStamp()
        {
            sfx.Play(library.folderOpen, SfxVolume);
            yield return new WaitForSecondsRealtime(0.3f);
            sfx.Play(library.typewriterBell, SfxVolume);
            yield return new WaitForSecondsRealtime(0.45f);
            sfx.Play(library.stamp, SfxVolume);
        }

        private void SetStressTheme(bool on)
        {
            if (_stressTheme == on) return;
            _stressTheme = on;
            music.Play(on ? library.musicStress : library.musicInterrogation, SoundChannel.GameMusic, 3f);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            var stress = mood != null && Playable ? mood.Stress01 : 0f;

            _heart.TargetVolume = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(heartbeatFrom, 1f, stress));
            _heart.TargetPitch = 0.9f + 0.6f * stress;
            _drone.TargetVolume = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(droneFrom, 1f, stress));

            if (Playable && Phase != GamePhase.Climax)
            {
                if (!_stressTheme && stress >= stressMusicOn) SetStressTheme(true);
                else if (_stressTheme && stress < stressMusicOff) SetStressTheme(false);
            }
            music.Duck = Playable && mood != null && mood.IsThinking ? thinkingDuck : 1f;

            if (Phase != GamePhase.Summary && Time.time >= _nextFoghorn)
            {
                _nextFoghorn = Time.time + Random.Range(foghornInterval.x, foghornInterval.y);
                sfx.Play(library.foghorn, AmbientVolume);
            }

            var ambient = AmbientVolume;
            _rain.Tick(dt, ambient);
            _room.Tick(dt, ambient);
            _lamp.Tick(dt, ambient);
            _drone.Tick(dt, ambient);
            _hiss.Tick(dt, ambient);
            _heart.Tick(dt, SfxVolume);
        }

        private void OnRecordingChanged(bool recording)
        {
            var pos = tape.transform.position;
            sfx.Play(recording ? library.tapeRec : library.tapeStop, SfxVolume, pos);
            _hiss.Transform.position = pos;
            _hiss.TargetVolume = recording ? 1f : 0f;
        }

        private void OnThinkingStarted()
        {
            if (Random.value < creakOnThinkingChance)
                sfx.Play(library.chairCreak, SfxVolume, SuspectPos);
        }

        private void OnTurnCompleted(TurnResult turn)
        {
            var reply = turn.Reply;
            if (reply == null) return;
            var newFact = Reveal(reply.RevealedFactId) | Reveal(turn.Guard.ExposedFactId);
            if (reply.Contradiction.Detected) sfx.Play(library.stingContradiction, SfxVolume);
            else if (newFact) sfx.Play(library.stingReveal, SfxVolume);
        }

        private bool Reveal(string factId) => !string.IsNullOrWhiteSpace(factId) && _revealed.Add(factId);

        private void OnGameEnded(InterrogationOutcome outcome) => _outcome = outcome;

        // A düh az asztalra csap (az „Outburst" animációval egy időben), az idegesség fészkelődik.
        private void OnEmotionChanged(SuspectEmotion emotion)
        {
            if (!Playable && Phase != GamePhase.Ending) return;
            if (emotion == SuspectEmotion.Hostile) sfx.Play(library.tableSlam, SfxVolume, SuspectPos);
            else if (emotion == SuspectEmotion.Nervous || emotion == SuspectEmotion.Breaking)
                sfx.Play(library.chairCreak, SfxVolume, SuspectPos);
        }

        private void OnStamp(string label) => sfx.Play(library.stamp, SfxVolume);

        private void OnToast() => sfx.Play(library.paper, SfxVolume);
    }
}
