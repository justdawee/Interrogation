using UnityEngine;

namespace Interrogation.Character
{
    /// <summary>
    /// A CC-kéz alapállása eltér attól, amit a Humanoid-retarget feltételez, ezért a klipekben a kéz
    /// közel állandó szöggel (~70–75°) el van fordulva. Az Animator után minden képkockában
    /// visszaforgatja egy kalibrált, a kéz anatómiai keretében megadott eltéréssel.
    /// </summary>
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(Animator))]
    public sealed class HandRetargetCorrection : MonoBehaviour
    {
        [Tooltip("A bal kéz korrekciója az anatómiai kéz-keretben (Euler, fok).")]
        [SerializeField] private Vector3 leftOffsetEuler;
        [Tooltip("A jobb kéz korrekciója az anatómiai kéz-keretben (Euler, fok).")]
        [SerializeField] private Vector3 rightOffsetEuler;

        private Transform _leftHand, _rightHand;
        private Quaternion _leftLocal = Quaternion.identity, _rightLocal = Quaternion.identity;

        public void SetOffsets(Vector3 left, Vector3 right)
        {
            leftOffsetEuler = left;
            rightOffsetEuler = right;
            if (_leftHand != null) Rebuild();
        }

        private void Awake()
        {
            var animator = GetComponent<Animator>();
            _leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            _rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Rebuild();
        }

        private void Rebuild()
        {
            var animator = GetComponent<Animator>();
            _leftLocal = ToBoneLocal(animator, true, Quaternion.Euler(leftOffsetEuler));
            _rightLocal = ToBoneLocal(animator, false, Quaternion.Euler(rightOffsetEuler));
        }

        /// <summary>
        /// Egy kézhez rögzített pont helye a korrekció után. A kar-IK ezzel számol előre,
        /// mert az IK-passz a korrekció előtt fut.
        /// </summary>
        public Vector3 Corrected(bool left, Vector3 point)
        {
            var hand = left ? _leftHand : _rightHand;
            if (hand == null || !isActiveAndEnabled) return point;
            var delta = hand.rotation * (left ? _leftLocal : _rightLocal) * Quaternion.Inverse(hand.rotation);
            return hand.position + delta * (point - hand.position);
        }

        private void LateUpdate()
        {
            if (_leftHand != null) _leftHand.localRotation *= _leftLocal;
            if (_rightHand != null) _rightHand.localRotation *= _rightLocal;
        }

        // A kéz anatómiai kerete a csont-térhez képest állandó, így az eltérés egyszer átszámolható: C·Q·C⁻¹.
        private static Quaternion ToBoneLocal(Animator animator, bool left, Quaternion offset)
        {
            var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (hand == null || !TryHandFrame(animator, left, out var frameWorld)) return Quaternion.identity;
            var c = Quaternion.Inverse(hand.rotation) * frameWorld;
            return c * offset * Quaternion.Inverse(c);
        }

        /// <summary>Világ-térben: előre = ujjak iránya, fel = tenyér-normális.</summary>
        public static bool TryHandFrame(Animator animator, bool left, out Quaternion frame)
        {
            frame = Quaternion.identity;
            var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var middle = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            var index = animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            var little = animator.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            if (hand == null || middle == null || index == null || little == null) return false;

            var fingers = (middle.position - hand.position).normalized;
            // A tenyér felé mutató normális (az ujjak ebbe az irányba hajlanak be).
            var palm = Vector3.Cross(index.position - little.position, fingers);
            if (!left) palm = -palm;
            palm = Vector3.ProjectOnPlane(palm, fingers);
            if (palm.sqrMagnitude < 1e-8f) return false;
            frame = Quaternion.LookRotation(fingers, palm.normalized);
            return true;
        }
    }
}
