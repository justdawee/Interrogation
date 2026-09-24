using Interrogation.Llm;
using UnityEngine;

namespace Interrogation.Character
{
    /// <summary>
    /// Tekintet és fejtartás: a detektívre néz, de a stressz, az érzelem és a gondolkodás szerint
    /// el-elnéz; a szem gyorsabban mozdul, mint a fej.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class SuspectGaze : MonoBehaviour
    {
        [SerializeField] private SuspectMood mood;
        [SerializeField] private SuspectFaceController face;
        [SerializeField] private Animator animator;

        [Tooltip("Kire néz. Üresen a fő kamera (a detektív nézőpontja).")]
        [SerializeField] private Transform target;

        [Header("Fej")]
        [SerializeField] private float maxHeadAngle = 40f;
        [Range(0f, 1f)] [SerializeField] private float neckShare = 0.4f;
        [SerializeField] private float headFollowSpeed = 4f;
        [Tooltip("A fej-csont helyi 'előre' és 'fel' iránya semleges pózban (kalibrált).")]
        [SerializeField] private Vector3 headLocalForward = Vector3.forward;
        [SerializeField] private Vector3 headLocalUp = Vector3.up;

        [Tooltip("Egyszeri reakció alatt a fej-követés ennyivel gyengül (1 = teljesen a klip vezet).")]
        [Range(0f, 1f)] [SerializeField] private float reactionRelease = 0.8f;

        [Header("Szem")]
        [SerializeField] private float maxEyeAngle = 25f;
        [SerializeField] private Vector3 eyeLocalForward = Vector3.down;

        [Header("Elnézés")]
        [Tooltip("Elnézés esélye másodpercenként nyugodt állapotban.")]
        [SerializeField] private float baseAvertChance = 0.04f;

        private Transform _neck, _head, _leftEye, _rightEye;
        private Vector3 _lookDir;
        private Vector3 _avertOffset;
        private float _avertUntil;
        private Vector3 _saccade;
        private float _nextSaccade;

        private void Awake()
        {
            if (mood == null) mood = GetComponent<SuspectMood>();
            if (face == null) face = GetComponent<SuspectFaceController>();
            if (animator == null) animator = GetComponent<Animator>();
            _neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            _head = animator.GetBoneTransform(HumanBodyBones.Head);
            _leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            _rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
        }

        private void Start()
        {
            if (target == null && Camera.main != null) target = Camera.main.transform;
            _lookDir = transform.forward;
        }

        private void LateUpdate()
        {
            if (_head == null || target == null) return;
            var focus = ComputeFocusPoint();
            // Reakció alatt a fej a klipet követi, a tekintet csak részben húzza a detektív felé.
            var weight = 1f - reactionRelease * SuspectReactionEnvelope.Weight(animator);

            var desired = (focus - _head.position).normalized;
            _lookDir = Vector3.Slerp(_lookDir, desired, 1f - Mathf.Exp(-headFollowSpeed * Time.deltaTime));
            var headFwd = _head.rotation * headLocalForward;
            var full = Limit(Quaternion.FromToRotation(headFwd, _lookDir), maxHeadAngle);
            if (_neck != null) _neck.rotation = Quaternion.Slerp(Quaternion.identity, full, neckShare * weight) * _neck.rotation;
            headFwd = _head.rotation * headLocalForward;
            var head = Limit(Quaternion.FromToRotation(headFwd, _lookDir), maxHeadAngle);
            _head.rotation = Quaternion.Slerp(Quaternion.identity, head, weight) * _head.rotation;
            KeepHeadLevel(weight);

            AimEye(_leftEye, focus + _saccade, weight);
            AimEye(_rightEye, focus + _saccade, weight);
        }

        private Vector3 ComputeFocusPoint()
        {
            var emotion = mood != null ? mood.Emotion : SuspectEmotion.Calm;
            var stress = mood != null ? mood.Stress01 : 0f;
            var thinking = mood != null && mood.IsThinking;
            var eyes = target.position;

            if (emotion == SuspectEmotion.Breaking)
                return _head.position + (Vector3.ProjectOnPlane(eyes - _head.position, Vector3.up).normalized * 0.6f) + Vector3.down * 0.55f;

            if (Time.time >= _avertUntil)
            {
                _avertOffset = Vector3.zero;
                var chance = baseAvertChance + stress * 0.25f + (thinking ? 0.6f : 0f) +
                             (emotion == SuspectEmotion.Nervous ? 0.25f : 0f) - (emotion == SuspectEmotion.Hostile ? 0.04f : 0f);
                if (Random.value < chance * Time.deltaTime)
                {
                    var side = Random.value < 0.5f ? -1f : 1f;
                    var right = Vector3.Cross(Vector3.up, (eyes - _head.position).normalized);
                    var down = thinking ? Random.Range(-0.1f, 0.25f) : Random.Range(0.2f, 0.5f);
                    _avertOffset = right * side * Random.Range(0.3f, 0.8f) + Vector3.down * down;
                    _avertUntil = Time.time + (thinking ? Random.Range(1.2f, 2.8f) : Random.Range(0.5f, 1.6f));
                    if (face != null) face.RequestBlink();
                }
                else _avertUntil = Time.time + 0.1f;
            }

            if (Time.time >= _nextSaccade)
            {
                _saccade = Random.insideUnitSphere * 0.02f;
                _nextSaccade = Time.time + Random.Range(0.3f, 1.2f);
            }

            // Apró Perlin-mozgás, hogy sose legyen merev.
            var chinUp = emotion == SuspectEmotion.Defensive ? Vector3.up * 0.08f : Vector3.zero;
            var sway = new Vector3(Mathf.PerlinNoise(Time.time * 0.3f, 1f) - 0.5f, Mathf.PerlinNoise(2f, Time.time * 0.25f) - 0.5f, 0f) * 0.06f;
            return eyes + _avertOffset + chinUp + sway;
        }

        private void KeepHeadLevel(float weight)
        {
            var fwd = _head.rotation * headLocalForward;
            var up = _head.rotation * headLocalUp;
            var wantUp = Vector3.ProjectOnPlane(Vector3.up, fwd);
            if (wantUp.sqrMagnitude < 1e-4f) return;
            var roll = Vector3.SignedAngle(Vector3.ProjectOnPlane(up, fwd), wantUp, fwd);
            _head.rotation = Quaternion.AngleAxis(roll * 0.8f * weight, fwd) * _head.rotation;
        }

        private void AimEye(Transform eye, Vector3 point, float weight)
        {
            if (eye == null) return;
            var fwd = eye.rotation * eyeLocalForward;
            var rot = Limit(Quaternion.FromToRotation(fwd, (point - eye.position).normalized), maxEyeAngle);
            eye.rotation = Quaternion.Slerp(Quaternion.identity, rot, weight) * eye.rotation;
        }

        private static Quaternion Limit(Quaternion rotation, float maxAngle)
        {
            rotation.ToAngleAxis(out var angle, out var axis);
            if (angle > 180f) angle -= 360f;
            if (float.IsNaN(axis.x) || Mathf.Abs(angle) < 0.01f) return Quaternion.identity;
            return Quaternion.AngleAxis(Mathf.Clamp(angle, -maxAngle, maxAngle), axis);
        }
    }
}
