using System.Collections.Generic;
using Interrogation.Llm;
using UnityEngine;

namespace Interrogation.Character
{
    /// <summary>
    /// Az arc egyetlen író-komponense: összegzi az érzelem-presetet, a stressz-rátétet, a gondolkodó
    /// arcot, a reakciókat, a pislogást és az ajakmozgást, majd egyszer írja ki a blendshape-eket.
    /// Az állkapcsot csontforgatással nyitja, így a fogak együtt mozognak.
    /// </summary>
    [DefaultExecutionOrder(20)]
    public sealed class SuspectFaceController : MonoBehaviour
    {
        [SerializeField] private SuspectMood mood;
        [SerializeField] private SuspectLipSync lipSync;
        [SerializeField] private Animator animator;

        [Header("Kifejezés")]
        [Tooltip("Az érzelem-váltás sebessége (1/s).")]
        [SerializeField] private float expressionSpeed = 3.5f;
        [Tooltip("A követés sebessége egyszeri reakció (döbbenet, düh, nevetés) alatt (1/s).")]
        [SerializeField] private float reactionExpressionSpeed = 10f;

        [Header("Pislogás")]
        [SerializeField] private Vector2 blinkIntervalCalm = new Vector2(2.5f, 6f);
        [SerializeField] private Vector2 blinkIntervalStressed = new Vector2(0.7f, 2.2f);
        [SerializeField] private float blinkDuration = 0.14f;

        [Header("Állkapocs")]
        [Tooltip("Az állkapocs-csont helyi tengelye, amely körül a pozitív szög NYITJA a szájat (kalibrált: -Z).")]
        [SerializeField] private Vector3 jawOpenAxis = new Vector3(0f, 0f, -1f);
        [SerializeField] private float jawMaxAngle = 14f;

        private FaceRig _rig;
        private Transform _jaw;
        private Quaternion _jawClosed;
        private readonly Dictionary<string, float> _expression = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _target = new Dictionary<string, float>();
        private readonly List<string> _managed = new List<string>();
        private float _nextBlink;
        private float _blinkStart = -1f;

        private void Awake()
        {
            if (mood == null) mood = GetComponent<SuspectMood>();
            if (lipSync == null) lipSync = GetComponent<SuspectLipSync>();
            if (animator == null) animator = GetComponent<Animator>();
            _rig = new FaceRig(GetComponentsInChildren<SkinnedMeshRenderer>(true));
            _jaw = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Jaw) : null;
            // A klipekben nincs állkapocs-görbe, ezért a Humanoid félig nyitott állásba tenné; ezt felülírjuk.
            if (_jaw != null) _jawClosed = _jaw.localRotation;

            foreach (var shape in EmotionFacePresets.AllShapes()) Manage(shape);
            Manage(EmotionFacePresets.BlinkLeft);
            Manage(EmotionFacePresets.BlinkRight);
            Manage(EmotionFacePresets.LipBite);
            foreach (var viseme in SuspectLipSync.Visemes) Manage(viseme);
            ScheduleBlink();
        }

        private void Manage(string shape)
        {
            if (!_rig.Has(shape) || _expression.ContainsKey(shape)) return;
            _managed.Add(shape);
            _expression[shape] = 0f;
        }

        public void RequestBlink()
        {
            if (_blinkStart < 0f) _blinkStart = Time.time;
        }

        private void LateUpdate()
        {
            var stress = mood != null ? mood.Stress01 : 0f;
            BuildTargets(stress);
            var reactionJaw = ApplyReaction(out var reactionWeight);

            // Reakció alatt gyorsabban követ, hogy a mimika együtt induljon a testtel.
            var speed = Mathf.Lerp(expressionSpeed, reactionExpressionSpeed, reactionWeight);
            var k = 1f - Mathf.Exp(-speed * Time.deltaTime);
            foreach (var shape in _managed)
            {
                _target.TryGetValue(shape, out var goal);
                _expression[shape] = Mathf.Lerp(_expression[shape], goal, k);
            }

            var speaking = lipSync != null ? Mathf.Clamp01(lipSync.JawOpen01 * 3f + (lipSync.IsSpeaking ? 0.5f : 0f)) : 0f;
            var blink = UpdateBlink(stress);

            foreach (var shape in _managed)
            {
                var w = _expression[shape];
                if (IsMouthShape(shape)) w *= 1f - 0.7f * speaking;
                if (shape == EmotionFacePresets.BlinkLeft || shape == EmotionFacePresets.BlinkRight)
                    w = Mathf.Max(w, blink);
                _rig.Set(shape, w);
            }

            var jawOpen = 0f;
            if (lipSync != null)
            {
                for (var i = 0; i < SuspectLipSync.Visemes.Length; i++)
                    _rig.Set(SuspectLipSync.Visemes[i], lipSync.Weights[i] * 0.85f);
                jawOpen = lipSync.JawOpen01;
            }
            jawOpen = Mathf.Max(jawOpen, reactionJaw);
            if (_jaw != null)
                _jaw.localRotation = _jawClosed * Quaternion.AngleAxis(jawOpen * jawMaxAngle, jawOpenAxis);
        }

        private void BuildTargets(float stress)
        {
            _target.Clear();
            var emotion = mood != null ? mood.Emotion : SuspectEmotion.Calm;
            foreach (var (shape, weight) in EmotionFacePresets.For(emotion))
                _target[shape] = weight;

            // Stressz: feszülő homlok, préselt száj; összeomláskor remegő ajak.
            Add(EmotionFacePresets.BrowInnerUp, stress * 0.25f);
            Add(EmotionFacePresets.MouthPressLeft, stress * 0.15f);
            Add(EmotionFacePresets.MouthPressRight, stress * 0.15f);
            if (emotion == SuspectEmotion.Breaking)
            {
                var quiver = (Mathf.PerlinNoise(Time.time * 7f, 0.3f) - 0.5f) * 0.3f;
                Add(EmotionFacePresets.LowerLipDownLeft, quiver);
                Add(EmotionFacePresets.LowerLipDownRight, quiver);
            }

            if (mood != null && mood.IsThinking)
            {
                Add(EmotionFacePresets.LipBite, 0.35f);
                Add(EmotionFacePresets.SquintLeft, 0.15f);
                Add(EmotionFacePresets.SquintRight, 0.15f);
            }
        }

        // A reakció súlyát az Animator átmenete adja, így a mimika a testtel együtt indul és ér véget.
        private float ApplyReaction(out float weight)
        {
            if (!SuspectReactionEnvelope.TryGet(animator, out var reaction, out weight, out var seconds) || weight <= 0f)
            {
                weight = 0f;
                return 0f;
            }

            var preset = EmotionFacePresets.For(reaction);
            foreach (var shape in _managed)
            {
                _target.TryGetValue(shape, out var current);
                var goal = 0f;
                foreach (var (s, w) in preset)
                    if (s == shape) { goal = w; break; }
                // A szemhéj-alapállást a reakció nem nullázza, a pislogás külön rétegen fut.
                if (goal <= 0f && (shape == EmotionFacePresets.BlinkLeft || shape == EmotionFacePresets.BlinkRight)) continue;
                _target[shape] = Mathf.Lerp(current, goal, weight);
            }
            return EmotionFacePresets.ReactionJaw(reaction, seconds) * weight;
        }

        private void Add(string shape, float value)
        {
            _target.TryGetValue(shape, out var current);
            _target[shape] = Mathf.Clamp01(current + value);
        }

        private float UpdateBlink(float stress)
        {
            if (_blinkStart < 0f && Time.time >= _nextBlink) _blinkStart = Time.time;
            if (_blinkStart < 0f) return 0f;

            var t = (Time.time - _blinkStart) / blinkDuration;
            if (t >= 1f)
            {
                _blinkStart = -1f;
                ScheduleBlink(stress);
                return 0f;
            }
            return Mathf.Sin(t * Mathf.PI);
        }

        private void ScheduleBlink(float stress = 0f)
        {
            var min = Mathf.Lerp(blinkIntervalCalm.x, blinkIntervalStressed.x, stress);
            var max = Mathf.Lerp(blinkIntervalCalm.y, blinkIntervalStressed.y, stress);
            _nextBlink = Time.time + Random.Range(min, max);
            if (Random.value < 0.12f) _nextBlink = Time.time + blinkDuration * 1.6f; // időnkénti dupla pislogás
        }

        private static bool IsMouthShape(string shape) =>
            shape.Contains("Mouth") || shape.Contains("Jaw") || shape == EmotionFacePresets.LipBite;
    }
}
