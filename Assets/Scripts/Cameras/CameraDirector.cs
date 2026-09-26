using Interrogation.Character;
using Interrogation.Flow;
using Interrogation.Llm;
using Interrogation.Game;
using Interrogation.Props;
using Unity.Cinemachine;
using UnityEngine;

namespace Interrogation.Cameras
{
    /// <summary>
    /// Cinemachine-beállítások váltása a játékállapot alapján (<see cref="ShotRules"/>), a stressz-vezérelt
    /// kamera-„légzés" és a dühkitörés rázása. A detektív-nézetet a <see cref="CameraLookAround"/> mozgatja.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraDirector : MonoBehaviour
    {
        private const int LivePriority = 20;
        private const int IdlePriority = 0;

        [Header("Hivatkozások")]
        [SerializeField] private CinemachineBrain brain;
        [SerializeField] private GameDirector director;
        [SerializeField] private InterrogationManager manager;
        [SerializeField] private SuspectMood mood;
        [SerializeField] private EvidencePropDirector props;

        [Header("Beállítások")]
        [SerializeField] private CinemachineCamera menuShot;
        [SerializeField] private CinemachineCamera detectiveShot;
        [SerializeField] private CinemachineCamera reactionShot;
        [SerializeField] private CinemachineCamera breakingShot;
        [SerializeField] private CinemachineCamera endingShot;

        [Header("Blendek (mp)")]
        [SerializeField] private float menuToSeat = 3.2f;
        [Tooltip("Az összeomlás lassú push-in-je.")]
        [SerializeField] private float pushIn = 6f;
        [SerializeField] private float release = 1.2f;
        [Tooltip("Vissza a detektív-nézetbe, ha felmutatás indul (a tárgy ahhoz igazodik).")]
        [SerializeField] private float presentReturn = 0.5f;
        [SerializeField] private float reactionIn = 0.4f;
        [SerializeField] private float reactionOut = 0.9f;
        [SerializeField] private float reactionHold = 2.2f;
        [SerializeField] private float toEnding = 4f;

        [Header("Kamera-légzés (Perlin-zaj)")]
        [SerializeField] private float calmAmplitude = 0.15f;
        [SerializeField] private float stressedAmplitude = 0.6f;
        [SerializeField] private float calmFrequency = 0.5f;
        [SerializeField] private float stressedFrequency = 1.1f;
        [Tooltip("A stressz-érték simítása (mp) — a légzés ne ugorjon körönként.")]
        [SerializeField] private float stressSmoothing = 1.5f;

        [Header("Dühkitörés-rázás")]
        [SerializeField] private float shakeAmplitude = 2.2f;
        [SerializeField] private float shakeFrequency = 3.5f;
        [Tooltip("A rázás lecsengési ideje (mp).")]
        [SerializeField] private float shakeDecay = 0.45f;

        private CinemachineCamera[] _all;
        private CinemachineBasicMultiChannelPerlin[] _noise;
        private CameraShot? _live;
        private InterrogationOutcome? _outcome;
        private float _reactionUntil = float.NegativeInfinity;
        private float _stress;
        private float _stressVelocity;
        private float _shake;

        public CameraShot? Live => _live;

        public bool IsBreaking => director != null &&
            ShotRules.IsBreaking(director.Phase, SuspectBreaking, _outcome);

        /// <summary>A blend még tart, vagy még el sem indult.</summary>
        public bool IsTransitioning =>
            brain != null && (brain.IsBlending || (_live.HasValue && (Object)brain.ActiveVirtualCamera != Shot(_live.Value)));

        private bool SuspectBreaking => mood != null && mood.Emotion == SuspectEmotion.Breaking;
        private bool Presenting => props != null && props.IsPresenting;

        private void Awake()
        {
            _all = new[] { menuShot, detectiveShot, reactionShot, breakingShot, endingShot };
            _noise = new CinemachineBasicMultiChannelPerlin[_all.Length];
            for (int i = 0; i < _all.Length; i++)
                if (_all[i] != null) _noise[i] = _all[i].GetComponent<CinemachineBasicMultiChannelPerlin>();
        }

        private void OnEnable()
        {
            if (director != null) director.PhaseChanged += OnPhaseChanged;
            if (manager != null) manager.GameEnded += OnGameEnded;
            if (mood != null) mood.ReactionRequested += OnReaction;
        }

        private void OnDisable()
        {
            if (director != null) director.PhaseChanged -= OnPhaseChanged;
            if (manager != null) manager.GameEnded -= OnGameEnded;
            if (mood != null) mood.ReactionRequested -= OnReaction;
        }

        private void Start() => Refresh();

        private void Update()
        {
            Refresh();
            UpdateNoise();
        }

        // Azonnal váltunk, hogy a blend még ebben a képkockában induljon (a rendező erre várhat).
        private void OnPhaseChanged(GamePhase from, GamePhase to) => Refresh();

        private void OnGameEnded(InterrogationOutcome outcome)
        {
            _outcome = outcome;
            _reactionUntil = float.NegativeInfinity;
        }

        private void OnReaction(SuspectReaction reaction)
        {
            if (director == null) return;
            if (reaction == SuspectReaction.Outburst) _shake = shakeAmplitude;
            if (reaction == SuspectReaction.Caught && ShotRules.AllowsReaction(director.Phase, Presenting))
                _reactionUntil = Time.time + reactionHold;
        }

        private void Refresh()
        {
            if (director == null) return;
            var target = ShotRules.BaseShot(director.Phase, SuspectBreaking, _outcome, Presenting);
            if (Time.time < _reactionUntil && ShotRules.AllowsReaction(director.Phase, Presenting))
                target = CameraShot.Reaction;
            if (_live == target) return;
            Show(target, _live.HasValue ? BlendSeconds(_live.Value, target) : 0f);
        }

        private float BlendSeconds(CameraShot from, CameraShot to)
        {
            if (to == CameraShot.Menu) return 0f;
            if (from == CameraShot.Menu) return menuToSeat;
            if (to == CameraShot.Reaction) return reactionIn;
            if (from == CameraShot.Reaction) return reactionOut;
            if (to == CameraShot.Breaking) return pushIn;
            if (to == CameraShot.Ending) return toEnding;
            return Presenting ? presentReturn : release;
        }

        private void Show(CameraShot shot, float blendSeconds)
        {
            var target = Shot(shot);
            if (target == null || brain == null) return;
            brain.DefaultBlend = blendSeconds > 0f
                ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blendSeconds)
                : new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            foreach (var cam in _all)
                if (cam != null) cam.Priority = cam == target ? LivePriority : IdlePriority;
            _live = shot;
        }

        private CinemachineCamera Shot(CameraShot shot)
        {
            switch (shot)
            {
                case CameraShot.Menu: return menuShot;
                case CameraShot.Reaction: return reactionShot;
                case CameraShot.Breaking: return breakingShot;
                case CameraShot.Ending: return endingShot;
                default: return detectiveShot;
            }
        }

        private void UpdateNoise()
        {
            var goal = mood != null ? mood.Stress01 : 0f;
            _stress = Mathf.SmoothDamp(_stress, goal, ref _stressVelocity, stressSmoothing);
            _shake = Mathf.MoveTowards(_shake, 0f, shakeAmplitude * Time.deltaTime / Mathf.Max(0.05f, shakeDecay));

            var shake01 = shakeAmplitude > 0f ? _shake / shakeAmplitude : 0f;
            var amplitude = (Mathf.Lerp(calmAmplitude, stressedAmplitude, _stress) + _shake) * Settings.VideoSettings.CameraMotionScale;
            var frequency = Mathf.Lerp(Mathf.Lerp(calmFrequency, stressedFrequency, _stress), shakeFrequency, shake01);
            foreach (var noise in _noise)
            {
                if (noise == null) continue;
                noise.AmplitudeGain = amplitude;
                noise.FrequencyGain = frequency;
            }
        }
    }
}
