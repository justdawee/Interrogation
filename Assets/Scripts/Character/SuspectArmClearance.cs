using UnityEngine;

namespace Interrogation.Character
{
    /// <summary>
    /// Kéz- és alkar-ütközés a saját testtel és az asztallal. A klipek vékonyabb alakra készültek, ezért
    /// a törzset a gerinclánc mentén elliptikus keresztmetszetekkel közelítjük, és a belógó kezet a has
    /// elé toljuk (a gesztus oldalirányú mozgása megmarad). A fej gömb, az asztallap tömb.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class SuspectArmClearance : MonoBehaviour
    {
        [Header("Törzs (keresztmetszet a gerinc-tengelyhez képest, m)")]
        [Tooltip("A törzs fél-szélessége (oldalirány).")]
        [SerializeField] private float torsoHalfWidth = 0.17f;
        [Tooltip("A hasfal/mellkas távolsága a gerinc-tengelytől előre.")]
        [SerializeField] private float torsoFrontDepth = 0.12f;
        [Tooltip("A hát távolsága a gerinc-tengelytől hátra.")]
        [SerializeField] private float torsoBackDepth = 0.15f;
        [Tooltip("A törzs alja a medence-csont alatt.")]
        [SerializeField] private float belowHips = 0.08f;

        [Header("Fej (gömb, hajjal együtt)")]
        [Tooltip("A fejgömb középpontja a fej-csont fölött, a nyak–fej tengely mentén (m).")]
        [SerializeField] private float headCenterUp = 0.09f;
        [SerializeField] private float headRadius = 0.12f;
        [Tooltip("A tenyér közepe és a tenyér felülete közti távolság (m).")]
        [SerializeField] private float palmThickness = 0.025f;

        [Header("Kar")]
        [Tooltip("A csukló (kéz-csont) és a tenyér/kézhát felülete közti távolság.")]
        [SerializeField] private float handRadius = 0.04f;
        [SerializeField] private float forearmRadius = 0.04f;
        [Tooltip("A kéz legfeljebb ennyivel tolható el (m).")]
        [SerializeField] private float maxPush = 0.2f;
        [Tooltip("A könyök kifelé fordításakor a hint ennyivel kerül a könyökön túlra (m).")]
        [SerializeField] private float elbowSwing = 0.1f;
        [Tooltip("A kéz korrekciójának simítása (1/s) — megakadályozza a hirtelen ugrást.")]
        [SerializeField] private float smoothing = 14f;

        [Header("Asztal (lap tömbként, sugárkövetéssel kimérve)")]
        [SerializeField] private float tableTopY = 0.776f;
        [Tooltip("Az asztallap (a kávával együtt) alsó síkja.")]
        [SerializeField] private float tableBottomY = 0.661f;
        [Tooltip("Az asztal gyanúsított felőli élének távolsága a referencia-medencétől, előre (m).")]
        [SerializeField] private float tableEdgeForward = 0.298f;
        [SerializeField] private float fingerRadius = 0.012f;
        [Tooltip("Ha a csukló legfeljebb ennyivel van a lap teteje alatt, a kéz „az asztalon van” (felfelé toljuk).")]
        [SerializeField] private float onTableTolerance = 0.06f;

        private static readonly float[] ForearmSamples = { 0.3f, 0.5f, 0.7f, 0.85f };

        private Animator _animator;
        private HandRetargetCorrection _handCorrection;
        private Transform[] _spine;
        private Transform _head, _neck;
        private readonly Vector3[] _axis = new Vector3[5];
        private Vector3 _seatRef;
        private Arm _left, _right;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _handCorrection = GetComponent<HandRetargetCorrection>();
            _spine = new[]
            {
                Bone(HumanBodyBones.Hips), Bone(HumanBodyBones.Spine), Bone(HumanBodyBones.Chest),
                Bone(HumanBodyBones.UpperChest) ?? Bone(HumanBodyBones.Chest), Bone(HumanBodyBones.Neck)
            };
            _seatRef = _spine[0].position;
            _head = Bone(HumanBodyBones.Head);
            _neck = Bone(HumanBodyBones.Neck);
            _left = new Arm(_animator, true);
            _right = new Arm(_animator, false);
        }

        private Transform Bone(HumanBodyBones bone) => _animator.GetBoneTransform(bone);

        /// <summary>Mennyire lóg a sugárral felfújt pont az asztallapba (0 = nem lóg bele).</summary>
        public float TableDepth(Vector3 point, float radius)
        {
            var forward = Vector3.Dot(point - _seatRef, transform.forward) - (tableEdgeForward - radius);
            var above = tableTopY + radius - point.y;
            var below = point.y - (tableBottomY - radius);
            return forward <= 0f || above <= 0f || below <= 0f ? 0f : Mathf.Min(forward, Mathf.Min(above, below));
        }

        // A kéz-retarget korrekció utáni hely, mert az IK a korrekció előtt fut.
        private Vector3 HandPoint(Arm arm, Transform bone) =>
            _handCorrection != null ? _handCorrection.Corrected(arm.Goal == AvatarIKGoal.LeftHand, bone.position) : bone.position;

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0) return;
            for (var i = 0; i < _axis.Length; i++) _axis[i] = _spine[i].position;
            _axis[0] -= transform.up * belowHips; // a törzs alja a medence alatt
            Solve(_left);
            Solve(_right);
        }

        private void Solve(Arm arm)
        {
            var hand = _animator.GetIKPosition(arm.Goal);
            var elbow = arm.Elbow.position;

            var push = TorsoPush(hand, handRadius, 1f);
            if (arm.Knuckle != null) push = Combine(push, TorsoPush(HandPoint(arm, arm.Knuckle), handRadius, 1f));
            foreach (var t in ForearmSamples)
                push = Combine(push, TorsoPush(Vector3.Lerp(elbow, hand, t), forearmRadius, t));
            push = Vector2.ClampMagnitude(push, maxPush);

            // A könyök-közeli alkar a bordák oldalát súrolhatja: a könyököt kifelé fordítjuk.
            var elbowOut = Mathf.Max(SideOverlap(elbow, forearmRadius), SideOverlap(Vector3.Lerp(elbow, hand, 0.15f), forearmRadius));

            var headPush = HeadPush(hand, arm.Knuckle != null ? HandPoint(arm, arm.Knuckle) : hand);

            var k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            arm.Push = Vector2.Lerp(arm.Push, push, k);
            arm.ElbowOut = Mathf.Lerp(arm.ElbowOut, elbowOut, k);
            arm.HeadPush = Vector3.Lerp(arm.HeadPush, headPush, k);

            var offset = transform.right * arm.Push.x + transform.forward * arm.Push.y + arm.HeadPush;
            arm.TablePush = Vector3.Lerp(arm.TablePush, TablePush(arm, hand, elbow, offset), k);
            offset += arm.TablePush;
            var target = hand + offset;
            // A hint csak futó kar-IK mellett hat, ezért könyök-korrekciónál a kéz-IK is aktív.
            var hintWeight = Mathf.Clamp01(arm.ElbowOut / 0.01f);
            var weight = Mathf.Max(hintWeight, Mathf.Clamp01(offset.magnitude / 0.01f));
            _animator.SetIKPositionWeight(arm.Goal, weight);
            _animator.SetIKPosition(arm.Goal, target);

            var side = Mathf.Sign(Vector3.Dot(elbow - _axis[1], transform.right));
            _animator.SetIKHintPositionWeight(arm.Hint, hintWeight);
            _animator.SetIKHintPosition(arm.Hint, elbow + transform.right * (side * (arm.ElbowOut + elbowSwing)));
        }

        // A fej gömb: a belógó kezet sugárirányban kifelé toljuk, így a tenyér a fejre simul.
        private Vector3 HeadPush(Vector3 wrist, Vector3 knuckle)
        {
            if (_head == null || _neck == null) return Vector3.zero;
            var up = (_head.position - _neck.position).normalized;
            var center = _head.position + up * headCenterUp;
            var reach = headRadius + palmThickness;
            var best = Vector3.zero;
            foreach (var point in new[] { Vector3.Lerp(wrist, knuckle, 0.5f), knuckle })
            {
                var d = point - center;
                var dist = d.magnitude;
                if (dist >= reach || dist < 1e-4f) continue;
                var push = d / dist * (reach - dist);
                if (push.sqrMagnitude > best.sqrMagnitude) best = push;
            }
            return Vector3.ClampMagnitude(best, maxPush);
        }

        // Előre-irányban a nagyobb igény számít; oldalirányban a nagyobb abszolút értékű.
        private static Vector2 Combine(Vector2 a, Vector2 b) =>
            new Vector2(Mathf.Abs(b.x) > Mathf.Abs(a.x) ? b.x : a.x, Mathf.Max(a.y, b.y));

        /// <summary>
        /// A szükséges elmozdítás (x: oldalra, y: előre), hogy a pont a törzsön kívülre kerüljön. A has előtt
        /// előre, a gerinc síkja mögött (pl. csípőre tett kéz) kifelé tol.
        /// </summary>
        /// <param name="leverage">A pont helye az alkaron (1 = csukló); a könyökhöz közeli pont kevésbé mozdul.</param>
        private Vector2 TorsoPush(Vector3 point, float radius, float leverage)
        {
            if (!AxisAt(point.y, out var center)) return Vector2.zero;
            var d = point - center;
            var x = Vector3.Dot(d, transform.right);
            var z = Vector3.Dot(d, transform.forward);
            var scale = 1f / Mathf.Max(0.3f, leverage);

            if (z < 0f) return new Vector2(Mathf.Sign(x) * SideOverlap(point, radius) * scale, 0f);

            var rx = torsoHalfWidth + radius;
            var across = x / rx;
            if (across * across >= 1f) return Vector2.zero;
            var surface = (torsoFrontDepth + radius) * Mathf.Sqrt(1f - across * across);
            return z >= surface ? Vector2.zero : new Vector2(0f, (surface - z) * scale);
        }

        // Oldalirányú átfedés: mennyivel kell a pontot kifelé tolni, hogy a törzs oldalán kívül legyen.
        private float SideOverlap(Vector3 point, float radius)
        {
            if (!AxisAt(point.y, out var center)) return 0f;
            var d = point - center;
            var x = Mathf.Abs(Vector3.Dot(d, transform.right));
            var z = Vector3.Dot(d, transform.forward);
            var rz = (z >= 0f ? torsoFrontDepth : torsoBackDepth) + radius;
            var along = z / rz;
            if (along * along >= 1f) return 0f;
            var surface = (torsoHalfWidth + radius) * Mathf.Sqrt(1f - along * along);
            return Mathf.Max(0f, surface - x);
        }

        // A gerinc-tengely pontja az adott magasságban (a csontlánc szakaszain interpolálva).
        private bool AxisAt(float y, out Vector3 center)
        {
            center = default;
            if (y < _axis[0].y || y > _axis[_axis.Length - 1].y) return false;
            for (var i = 0; i < _axis.Length - 1; i++)
            {
                if (y > _axis[i + 1].y) continue;
                center = Vector3.Lerp(_axis[i], _axis[i + 1], Mathf.InverseLerp(_axis[i].y, _axis[i + 1].y, y));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Asztalon lévő kéznél a belógó pontokat a lap fölé emeli; az ölben pihenő kezet hátrébb húzza,
        /// hogy az alkar az asztalél mögött haladjon.
        /// </summary>
        /// <param name="offset">A törzs/fej miatt már eldöntött korrekció.</param>
        private Vector3 TablePush(Arm arm, Vector3 hand, Vector3 elbow, Vector3 offset)
        {
            // Asztalon van a kéz, ha a csukló a lap közelében, vagy az él előtt a lap vastagságán belül van.
            var wrist = hand + offset;
            var wristForward = Vector3.Dot(wrist - _seatRef, transform.forward);
            var onTable = wrist.y > tableTopY - onTableTolerance || (wrist.y > tableBottomY && wristForward > tableEdgeForward);
            var up = 0f;
            var back = 0f;

            void Check(Vector3 point, float radius, float leverage)
            {
                var forward = Vector3.Dot(point - _seatRef, transform.forward);
                if (forward <= tableEdgeForward - radius || point.y >= tableTopY + radius || point.y <= tableBottomY - radius) return;
                var scale = 1f / Mathf.Max(0.3f, leverage);
                if (onTable) up = Mathf.Max(up, (tableTopY + radius - point.y) * scale);
                else back = Mathf.Max(back, (forward - (tableEdgeForward - radius)) * scale);
            }

            Check(wrist, handRadius, 1f);
            // Az ujjhegyek lapra simulását a SuspectFingerRelax végzi.
            if (arm.Knuckle != null) Check(HandPoint(arm, arm.Knuckle) + offset, fingerRadius, 1f);
            foreach (var t in ForearmSamples)
                Check(Vector3.Lerp(elbow, wrist, t), forearmRadius, t);

            return Vector3.ClampMagnitude(Vector3.up * up - transform.forward * back, maxPush);
        }

        private sealed class Arm
        {
            public readonly AvatarIKGoal Goal;
            public readonly AvatarIKHint Hint;
            public readonly Transform Elbow;
            public readonly Transform Knuckle;
            public Vector2 Push;
            public float ElbowOut;
            public Vector3 HeadPush;
            public Vector3 TablePush;

            public Arm(Animator animator, bool left)
            {
                HumanBodyBones B(HumanBodyBones l, HumanBodyBones r) => left ? l : r;
                Goal = left ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;
                Hint = left ? AvatarIKHint.LeftElbow : AvatarIKHint.RightElbow;
                Elbow = animator.GetBoneTransform(B(HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm));
                Knuckle = animator.GetBoneTransform(B(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal));
            }
        }
    }
}
