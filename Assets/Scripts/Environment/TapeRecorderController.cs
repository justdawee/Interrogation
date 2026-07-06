using UnityEngine;

namespace Interrogation.Rendering
{
    /// <summary>
    /// Az orsós magnó felvétel-animációja: az orsók forognak, a két VU-mutató együtt leng.
    /// Az importált alkatrészek tengelyei nem egyenesek, ezért szerkesztőben kimért pózokkal dolgozik;
    /// az „A" mutató gimbal-lock pózban áll, ezért a mutatókat Euler helyett Slerp mozgatja.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TapeRecorderController : MonoBehaviour
    {
        [Header("Reels")]
        [Tooltip("The left reel. Auto-bound to child 'PI_REC_WHEEL_A' if left empty.")]
        public Transform wheelA;
        [Tooltip("The right reel. Auto-bound to child 'PI_REC_WHEEL_B' if left empty.")]
        public Transform wheelB;
        [Tooltip("Reel spin speed in degrees/second at full speed.")]
        [Min(0f)] public float wheelSpeed = 150f;
        [Tooltip("Reverse the spin direction.")]
        public bool spinReversed = false;
        [Tooltip("Seconds for the reels to spin up to / down from full speed.")]
        [Min(0.01f)] public float wheelSpinUpTime = 0.6f;
        [Tooltip("Fixed local Y euler for the reels (the mounting that spins true on the X axis).")]
        public float wheelFixedY = -90f;
        [Tooltip("Fixed local Z euler for the reels.")]
        public float wheelFixedZ = 90f;

        [Header("VU meter needle A (sweeps local X)")]
        [Tooltip("Auto-bound to child 'PI_REC_METER_A' if left empty.")]
        public Transform meterA;
        [Tooltip("Needle A local euler at idle / rest (20 dB, quiet).")]
        public Vector3 meterAIdleEuler = new Vector3(-20f, -90f, 90f);
        [Tooltip("Needle A local euler at full signal (0 dB, red).")]
        public Vector3 meterAFullEuler = new Vector3(-100f, -90f, 90f);

        [Header("VU meter needle B (sweeps local Z)")]
        [Tooltip("Auto-bound to child 'PI_REC_METER_B' if left empty.")]
        public Transform meterB;
        [Tooltip("Needle B local euler at idle / rest (Z = -40, left, 20 dB).")]
        public Vector3 meterBIdleEuler = new Vector3(0f, 0f, -40f);
        [Tooltip("Needle B local euler at full signal (Z = +40, right, red 0 dB).")]
        public Vector3 meterBFullEuler = new Vector3(0f, 0f, 40f);

        [Header("VU meter motion")]
        [Tooltip("How fast the noise target changes. Higher = twitchier needles.")]
        [Min(0f)] public float needleNoiseSpeed = 2.4f;
        [Tooltip("Needle follow smoothing time in seconds. Lower = snappier.")]
        [Min(0.01f)] public float needleSmoothTime = 0.07f;

        [Header("Startup")]
        [Tooltip("Start recording automatically on Play. Temporary convenience until the " +
                 "interrogation FSM drives StartRecording()/StopRecording().")]
        public bool startRecordingOnPlay = true;

        public bool IsRecording { get; private set; }

        /// <summary>0..1, a mutatók kitérésének szorzója (beszéd-amplitúdóra köthető).</summary>
        [System.NonSerialized] public float Level = 1f;

        float _wheelAngle;
        float _wheelRamp;  // simított 0..1, hogy az orsók fokozatosan induljanak és álljanak meg

        // A két mutató egy csatornapár, ezért közös pozícióból mozog.
        float _needleT;
        float _needleVelocity;
        float _needleSeed;

        Needle _needleA;
        Needle _needleB;

        sealed class Needle
        {
            public Transform Transform;
            public Quaternion IdleRotation;
            public Quaternion FullRotation;
        }

        void Awake()
        {
            AutoBind();
            _needleSeed = Random.value * 1000f;
            _needleA = MakeNeedle(meterA, meterAIdleEuler, meterAFullEuler);
            _needleB = MakeNeedle(meterB, meterBIdleEuler, meterBFullEuler);
        }

        void Start()
        {
            if (startRecordingOnPlay)
                StartRecording();
        }

        /// <summary>Indításkor true, leállításkor false.</summary>
        public event System.Action<bool> RecordingChanged;

        public void StartRecording() => SetRecording(true);
        public void StopRecording() => SetRecording(false);

        void SetRecording(bool recording)
        {
            if (IsRecording == recording)
                return;
            IsRecording = recording;
            RecordingChanged?.Invoke(recording);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            float targetRamp = IsRecording ? 1f : 0f;
            _wheelRamp = Mathf.MoveTowards(_wheelRamp, targetRamp, dt / wheelSpinUpTime);

            float dir = spinReversed ? -1f : 1f;
            _wheelAngle += wheelSpeed * dir * _wheelRamp * dt;
            _wheelAngle = Mathf.Repeat(_wheelAngle, 360f);

            var reelEuler = new Vector3(_wheelAngle, wheelFixedY, wheelFixedZ);
            if (wheelA != null) wheelA.localEulerAngles = reelEuler;
            if (wheelB != null) wheelB.localEulerAngles = reelEuler;

            UpdateNeedles(dt);
        }

        void UpdateNeedles(float dt)
        {
            float target = 0f;
            if (IsRecording)
            {
                float noise = Mathf.PerlinNoise(_needleSeed, Time.time * needleNoiseSpeed);
                target = Mathf.Clamp01(noise * Mathf.Clamp01(Level));
            }

            _needleT = Mathf.SmoothDamp(_needleT, target, ref _needleVelocity, needleSmoothTime);
            float t = Mathf.Clamp01(_needleT);

            ApplyNeedle(_needleA, t);
            ApplyNeedle(_needleB, t);
        }

        static void ApplyNeedle(Needle needle, float t)
        {
            if (needle?.Transform == null)
                return;

            needle.Transform.localRotation = Quaternion.Slerp(needle.IdleRotation, needle.FullRotation, t);
        }

        static Needle MakeNeedle(Transform t, Vector3 idleEuler, Vector3 fullEuler)
        {
            if (t == null)
                return null;

            return new Needle
            {
                Transform = t,
                IdleRotation = Quaternion.Euler(idleEuler),
                FullRotation = Quaternion.Euler(fullEuler),
            };
        }

        void AutoBind()
        {
            if (wheelA == null) wheelA = transform.Find("PI_REC_WHEEL_A");
            if (wheelB == null) wheelB = transform.Find("PI_REC_WHEEL_B");
            if (meterA == null) meterA = transform.Find("PI_REC_METER_A");
            if (meterB == null) meterB = transform.Find("PI_REC_METER_B");
        }
    }
}
