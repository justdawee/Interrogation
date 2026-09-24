using System.Collections.Generic;
using UnityEngine;

namespace Interrogation.Character
{
    /// <summary>
    /// A Character Creator-rig csavarodó és „share" csontjait a Humanoid-animáció nem mozgatja, ezért a
    /// csukló „kitörtnek" látszik, a könyök beesik. Minden képkockában szétosztja a kéz csavarodását az
    /// alkaron, ellensúlyozza a felkarét, és a könyök/térd share-csontot a hajlítás felére forgatja.
    /// </summary>
    [DefaultExecutionOrder(30)]
    [RequireComponent(typeof(Animator))]
    public sealed class CcTwistBoneDriver : MonoBehaviour
    {
        [Header("Alkar (a kéz csavarodásának hányada)")]
        [Range(0f, 1f)] [SerializeField] private float forearmTwist01 = 0.25f;
        [Range(0f, 1f)] [SerializeField] private float forearmTwist02 = 0.65f;

        [Header("Felkar (a felkar csavarodásának ellensúlya)")]
        [Range(0f, 1f)] [SerializeField] private float upperarmTwist01 = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float upperarmTwist02 = 0.25f;

        [Header("Share-csontok (a hajlítás hányada)")]
        [Range(0f, 1f)] [SerializeField] private float shareBend = 0.5f;

        private readonly List<Limb> _limbs = new List<Limb>();

        private void Awake()
        {
            var animator = GetComponent<Animator>();
            foreach (var side in new[] { "L", "R" })
            {
                var left = side == "L";
                var upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
                var fore = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                _limbs.Add(new Limb(upper, fore, hand,
                    Find(upper, $"CC_Base_{side}_UpperarmTwist01"), Find(upper, $"CC_Base_{side}_UpperarmTwist02"),
                    Find(fore, $"CC_Base_{side}_ForearmTwist01"), Find(fore, $"CC_Base_{side}_ForearmTwist02"),
                    Find(fore, $"CC_Base_{side}_ElbowShareBone")));

                var thigh = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
                var calf = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                _limbs.Add(new Limb(thigh, calf, null, null, null, null, null, Find(calf, $"CC_Base_{side}_KneeShareBone")));
            }
        }

        private static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private void LateUpdate()
        {
            foreach (var limb in _limbs) limb.Apply(this);
        }

        // Swing–twist bontás: előjeles csavarodás (fok) a tengely körül.
        private static float TwistAngle(Quaternion rotation, Vector3 axis)
        {
            var v = new Vector3(rotation.x, rotation.y, rotation.z);
            var projected = Vector3.Dot(v, axis);
            var angle = 2f * Mathf.Atan2(projected, rotation.w) * Mathf.Rad2Deg;
            return Mathf.DeltaAngle(0f, angle);
        }

        private sealed class Limb
        {
            private readonly Transform _upper, _lower, _hand, _upTw1, _upTw2, _foTw1, _foTw2, _share;
            private readonly Quaternion _upperBind, _lowerBind, _handBind, _upTw1Bind, _upTw2Bind, _foTw1Bind, _foTw2Bind, _shareBind;
            private readonly Vector3 _upperAxisInParent, _lowerAxis;

            public Limb(Transform upper, Transform lower, Transform hand, Transform upTw1, Transform upTw2,
                        Transform foTw1, Transform foTw2, Transform share)
            {
                _upper = upper; _lower = lower; _hand = hand;
                _upTw1 = upTw1; _upTw2 = upTw2; _foTw1 = foTw1; _foTw2 = foTw2; _share = share;
                _upperBind = upper != null ? upper.localRotation : Quaternion.identity;
                _lowerBind = lower != null ? lower.localRotation : Quaternion.identity;
                _handBind = hand != null ? hand.localRotation : Quaternion.identity;
                _upTw1Bind = Bind(upTw1); _upTw2Bind = Bind(upTw2); _foTw1Bind = Bind(foTw1); _foTw2Bind = Bind(foTw2); _shareBind = Bind(share);
                // A CC-csontok hossztengelye a helyi +Y.
                _upperAxisInParent = _upperBind * Vector3.up;
                _lowerAxis = Vector3.up;
            }

            private static Quaternion Bind(Transform t) => t != null ? t.localRotation : Quaternion.identity;

            public void Apply(CcTwistBoneDriver d)
            {
                if (_upper == null || _lower == null) return;

                if (_hand != null && (_foTw1 != null || _foTw2 != null))
                {
                    var handDelta = _hand.localRotation * Quaternion.Inverse(_handBind);
                    var roll = TwistAngle(handDelta, _lowerAxis);
                    if (_foTw1 != null) _foTw1.localRotation = Quaternion.AngleAxis(roll * d.forearmTwist01, _lowerAxis) * _foTw1Bind;
                    if (_foTw2 != null) _foTw2.localRotation = Quaternion.AngleAxis(roll * (d.forearmTwist02 - d.forearmTwist01), _lowerAxis) * _foTw2Bind;
                }

                if (_upTw1 != null || _upTw2 != null)
                {
                    var upperDelta = _upper.localRotation * Quaternion.Inverse(_upperBind);
                    var roll = TwistAngle(upperDelta, _upperAxisInParent);
                    if (_upTw1 != null) _upTw1.localRotation = Quaternion.AngleAxis(-roll * d.upperarmTwist01, Vector3.up) * _upTw1Bind;
                    if (_upTw2 != null) _upTw2.localRotation = Quaternion.AngleAxis(roll * (d.upperarmTwist01 - d.upperarmTwist02), Vector3.up) * _upTw2Bind;
                }

                if (_share != null)
                {
                    var half = Quaternion.Slerp(_lowerBind, _lower.localRotation, 1f - d.shareBend);
                    _share.localRotation = Quaternion.Inverse(_lower.localRotation) * half * _shareBind;
                }
            }
        }
    }
}
