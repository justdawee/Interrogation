using System.Collections.Generic;
using UnityEngine;

namespace Interrogation.Character
{
    /// <summary>
    /// A klipek ujjtartása a CC-kézen merev vagy túlhajlított, ezért egy laza nyugalmi tartás felé húzza.
    /// Ha az ujjhegyek az asztallapba lógnának, előbb a csuklót hajlítja, majd az ujjláncot egyenesíti ki.
    /// </summary>
    [DefaultExecutionOrder(25)]
    [RequireComponent(typeof(Animator))]
    public sealed class SuspectFingerRelax : MonoBehaviour
    {
        [Tooltip("0 = a klip ujjmozgása változatlan, 1 = mindig a nyugalmi tartás.")]
        [Range(0f, 1f)] [SerializeField] private float fingerBlend = 0.55f;
        [Range(0f, 1f)] [SerializeField] private float thumbBlend = 0.7f;

        [Header("Nyugalmi tartás (Humanoid izom-érték, negatív = hajlított)")]
        [SerializeField] private float proximalCurl = -0.25f;
        [SerializeField] private float middleCurl = -0.35f;
        [SerializeField] private float distalCurl = -0.3f;
        [SerializeField] private float thumbCurl = -0.15f;

        [Header("Asztal-kontakt")]
        [Tooltip("A kiegyenesített (lapra simuló) ujjtartás izom-értéke.")]
        [SerializeField] private float flatCurl = 0.15f;
        [SerializeField] private float fingertipRadius = 0.01f;
        [Tooltip("Az asztal geometriáját adó komponens (üresen ugyanezen az objektumon keresi).")]
        [SerializeField] private SuspectArmClearance table;

        [Tooltip("A csukló legfeljebb ennyi fokkal hajlik, hogy a kéz a lapra feküdjön.")]
        [SerializeField] private float maxWristLift = 50f;

        private const int SearchSteps = 6;

        private readonly List<Chain> _chains = new List<Chain>();
        private Animator _animator;
        private Transform _leftHand, _rightHand;

        private void Awake()
        {
            var animator = _animator = GetComponent<Animator>();
            _leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            _rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (table == null) table = GetComponent<SuspectArmClearance>();
            var relaxed = ComputeFingerPose(animator, proximalCurl, middleCurl, distalCurl, thumbCurl);
            var flat = ComputeFingerPose(animator, flatCurl, flatCurl, flatCurl, flatCurl);
            for (var b = HumanBodyBones.LeftThumbProximal; b <= HumanBodyBones.RightLittleDistal; b += 3)
            {
                var chain = new Chain(b.ToString().Contains("Thumb") ? thumbBlend : fingerBlend, b <= HumanBodyBones.LeftLittleDistal);
                for (var i = 0; i < 3; i++)
                {
                    var bone = animator.GetBoneTransform(b + i);
                    if (bone == null || !relaxed.TryGetValue(bone, out var r) || !flat.TryGetValue(bone, out var f)) break;
                    chain.Add(bone, r, f);
                }
                if (chain.Count == 3) _chains.Add(chain);
            }
        }

        // Az ujj-izmokat beállítjuk, kiolvassuk a csontforgásokat, majd a csontvázat visszaállítjuk.
        private Dictionary<Transform, Quaternion> ComputeFingerPose(Animator animator, float proximal, float middle, float distal, float thumb)
        {
            var bones = GetComponentsInChildren<Transform>(true);
            var savedRot = new Quaternion[bones.Length];
            var savedPos = new Vector3[bones.Length];
            for (var i = 0; i < bones.Length; i++) { savedRot[i] = bones[i].localRotation; savedPos[i] = bones[i].localPosition; }

            var result = new Dictionary<Transform, Quaternion>();
            using (var handler = new HumanPoseHandler(animator.avatar, transform))
            {
                var pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                var names = HumanTrait.MuscleName;
                for (var i = 0; i < names.Length; i++)
                {
                    var n = names[i];
                    if (!n.Contains("Stretched")) continue;
                    pose.muscles[i] = n.Contains("Thumb") ? thumb
                        : n.Contains(" 1 ") ? proximal
                        : n.Contains(" 2 ") ? middle
                        : distal;
                }
                handler.SetHumanPose(ref pose);
                for (var b = HumanBodyBones.LeftThumbProximal; b <= HumanBodyBones.RightLittleDistal; b++)
                {
                    var bone = animator.GetBoneTransform(b);
                    if (bone != null) result[bone] = bone.localRotation;
                }
            }

            for (var i = 0; i < bones.Length; i++) { bones[i].localRotation = savedRot[i]; bones[i].localPosition = savedPos[i]; }
            return result;
        }

        private void LateUpdate()
        {
            foreach (var chain in _chains) chain.Relax();
            if (table == null) return;
            LiftWrist(_leftHand, true);
            LiftWrist(_rightHand, false);
            foreach (var chain in _chains) chain.RestOnTable(table, fingertipRadius);
        }

        // Mindkét irányt (tenyér a lapra / lelógó kéz) felezéssel keressük, és a kisebb szöget választjuk.
        private void LiftWrist(Transform hand, bool left)
        {
            if (hand == null || DeepestTip(left) <= 0f) return;
            var knuckle = _animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            if (knuckle == null) return;
            var fingers = (knuckle.position - hand.position).normalized;
            var axis = Vector3.Cross(fingers, Vector3.up);
            if (axis.sqrMagnitude < 1e-6f) return;
            axis.Normalize();

            var original = hand.rotation;
            var (upAngle, upDepth) = SearchWristAngle(hand, left, axis, original, 1f);
            var (downAngle, downDepth) = SearchWristAngle(hand, left, axis, original, -1f);
            var useUp = upDepth < downDepth || (Mathf.Approximately(upDepth, downDepth) && upAngle <= downAngle);
            hand.rotation = Quaternion.AngleAxis(useUp ? upAngle : -downAngle, axis) * original;
        }

        private (float angle, float depth) SearchWristAngle(Transform hand, bool left, Vector3 axis, Quaternion original, float sign)
        {
            hand.rotation = Quaternion.AngleAxis(sign * maxWristLift, axis) * original;
            var depthAtMax = DeepestTip(left);
            if (depthAtMax > 0f) return (maxWristLift, depthAtMax);

            float lo = 0f, hi = maxWristLift;
            for (var step = 0; step < SearchSteps; step++)
            {
                var mid = (lo + hi) * 0.5f;
                hand.rotation = Quaternion.AngleAxis(sign * mid, axis) * original;
                if (DeepestTip(left) > 0f) lo = mid; else hi = mid;
            }
            return (hi, 0f);
        }

        private float DeepestTip(bool left)
        {
            var deepest = 0f;
            foreach (var chain in _chains)
                if (chain.Left == left) deepest = Mathf.Max(deepest, table.TableDepth(chain.Tip(), fingertipRadius));
            return deepest;
        }

        // Egy ujj három perce a két célpózzal.
        private sealed class Chain
        {
            private readonly float _weight;
            private readonly Transform[] _bones = new Transform[3];
            private readonly Quaternion[] _relaxed = new Quaternion[3];
            private readonly Quaternion[] _flat = new Quaternion[3];
            private readonly Quaternion[] _current = new Quaternion[3];

            public int Count { get; private set; }
            public bool Left { get; }

            public Chain(float weight, bool left)
            {
                _weight = weight;
                Left = left;
            }

            public void Add(Transform bone, Quaternion relaxed, Quaternion flat)
            {
                _bones[Count] = bone;
                _relaxed[Count] = relaxed;
                _flat[Count] = flat;
                Count++;
            }

            public void Relax()
            {
                for (var i = 0; i < 3; i++)
                    _bones[i].localRotation = Quaternion.Slerp(_bones[i].localRotation, _relaxed[i], _weight);
            }

            // A legkisebb kiegyenesítés felezéssel, amelynél az ujjhegy már a lap fölött van.
            public void RestOnTable(SuspectArmClearance table, float radius)
            {
                if (table.TableDepth(Tip(), radius) <= 0f) return;
                for (var i = 0; i < 3; i++) _current[i] = _bones[i].localRotation;

                float lo = 0f, hi = 1f;
                for (var step = 0; step < SearchSteps; step++)
                {
                    var mid = (lo + hi) * 0.5f;
                    Apply(mid);
                    if (table.TableDepth(Tip(), radius) > 0f) lo = mid; else hi = mid;
                }
                Apply(hi);
            }

            private void Apply(float t)
            {
                for (var i = 0; i < 3; i++)
                    _bones[i].localRotation = Quaternion.Slerp(_current[i], _flat[i], t);
            }

            // Becslés: a disztális perc a középső perc irányában ugyanolyan hosszan folytatódik.
            public Vector3 Tip() => _bones[2].position * 2f - _bones[1].position;
        }
    }
}
