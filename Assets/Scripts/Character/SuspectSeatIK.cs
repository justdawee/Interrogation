using UnityEngine;

namespace Interrogation.Character
{
    /// <summary>
    /// Ülés-igazítás: a klipek eltérő ülésmagasságát kezeli. A medencét úgy emeli, hogy a comb alja az
    /// ülőlapra essen, a lábfejet a padlóhoz rögzíti, és ha a hát a támlába dőlne, a gerincet előrebillenti.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class SuspectSeatIK : MonoBehaviour
    {
        [Header("Ülőlap")]
        [SerializeField] private float seatTopY = 0.48f;
        [Tooltip("A combcsont és a comb alsó bőrfelülete közti távolság (mérve: ~0,10–0,12 m).")]
        [SerializeField] private float thighFlesh = 0.105f;
        [Tooltip("A medence-magasság követési sebessége (1/s) — nagyobb = gyorsabb, kisebb = simább.")]
        [SerializeField] private float seatFollow = 10f;

        [Header("Támla (a medence referenciapontja mögött, a karakter előre-irányával szemben)")]
        [SerializeField] private Vector2 backrestLow = new Vector2(0.55f, 0.15f);   // (magasság, távolság)
        [SerializeField] private Vector2 backrestHigh = new Vector2(0.95f, 0.215f);
        [Tooltip("A hát bőrfelülete ennyivel van a gerinccsontok mögött (m).")]
        [SerializeField] private float backThickness = 0.12f;
        [SerializeField] private float maxBackCorrection = 25f;

        [Header("Súlyok")]
        [Range(0f, 1f)] [SerializeField] private float pelvisPin = 1f;
        [Range(0f, 1f)] [SerializeField] private float feetPin = 1f;

        private Animator _animator;
        private Transform _hips, _spine, _chest, _upperChest, _neck;
        private Transform _lThigh, _lKnee, _rThigh, _rKnee;
        private Vector3 _seatRef;
        private float _hipsY;
        private Vector3 _leftFoot, _rightFoot;
        private Vector3 _hipsMinusBody;
        private Vector3 _finalBody;
        private bool _hasBody, _hasOffset;
        private float _backAngle;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _hips = Bone(HumanBodyBones.Hips);
            _spine = Bone(HumanBodyBones.Spine);
            _chest = Bone(HumanBodyBones.Chest);
            _upperChest = Bone(HumanBodyBones.UpperChest);
            _neck = Bone(HumanBodyBones.Neck);
            _lThigh = Bone(HumanBodyBones.LeftUpperLeg);
            _lKnee = Bone(HumanBodyBones.LeftLowerLeg);
            _rThigh = Bone(HumanBodyBones.RightUpperLeg);
            _rKnee = Bone(HumanBodyBones.RightLowerLeg);
            // Az Animator első kiértékelése előtt olvasva: a mentett ülő póz a referencia.
            _seatRef = _hips.position;
            _hipsY = _seatRef.y;
            _leftFoot = Bone(HumanBodyBones.LeftFoot).position;
            _rightFoot = Bone(HumanBodyBones.RightFoot).position;
        }

        private Transform Bone(HumanBodyBones bone) => _animator.GetBoneTransform(bone);

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0) return;

            // A csípő–test eltolást az előző képkockából becsüljük (1 képkocka késés, de stabil).
            if (_hasOffset && pelvisPin > 0f)
            {
                var wanted = new Vector3(_seatRef.x, _hipsY, _seatRef.z) - _hipsMinusBody;
                _animator.bodyPosition = Vector3.Lerp(_animator.bodyPosition, wanted, pelvisPin);
            }
            // A test-pozíció csak az IK-passzban olvasható.
            _finalBody = _animator.bodyPosition;
            _hasBody = true;

            PinFoot(AvatarIKGoal.LeftFoot, _leftFoot);
            PinFoot(AvatarIKGoal.RightFoot, _rightFoot);
        }

        private void LateUpdate()
        {
            if (!_hasBody) return;
            _hipsMinusBody = _hips.position - _finalBody;
            _hasOffset = true;
            UpdateSeatHeight();
            KeepBackOffBackrest();
        }

        // A comb legalsó pontját a combcsont első feléből becsüljük; a hibát simán korrigáljuk.
        private void UpdateSeatHeight()
        {
            var lowest = float.MaxValue;
            foreach (var t in new[] { 0f, 0.25f, 0.5f })
            {
                lowest = Mathf.Min(lowest, Vector3.Lerp(_lThigh.position, _lKnee.position, t).y);
                lowest = Mathf.Min(lowest, Vector3.Lerp(_rThigh.position, _rKnee.position, t).y);
            }
            var error = seatTopY - (lowest - thighFlesh);
            var target = _hips.position.y + error;
            _hipsY = Mathf.Lerp(_hipsY, target, 1f - Mathf.Exp(-seatFollow * Time.deltaTime));
        }

        // Ha a hát bőrfelülete a támla mögé kerülne, a gerincet előrebillentjük (simítva).
        private void KeepBackOffBackrest()
        {
            if (_spine == null) return;
            var forward = transform.forward;
            var needed = 0f;
            foreach (var bone in new[] { _chest, _upperChest, _neck })
            {
                if (bone == null) continue;
                var behind = Vector3.Dot(_seatRef - bone.position, forward) + backThickness;
                var limit = BackrestDistance(bone.position.y);
                var depth = behind - limit;
                if (depth <= 0f) continue;
                var height = Mathf.Max(0.1f, bone.position.y - _spine.position.y);
                needed = Mathf.Max(needed, Mathf.Atan2(depth, height) * Mathf.Rad2Deg);
            }
            _backAngle = Mathf.Lerp(_backAngle, Mathf.Min(needed, maxBackCorrection), 1f - Mathf.Exp(-12f * Time.deltaTime));
            if (_backAngle < 0.05f) return;
            var axis = Vector3.Cross(Vector3.up, forward);
            _spine.rotation = Quaternion.AngleAxis(_backAngle, axis) * _spine.rotation;
        }

        private float BackrestDistance(float y) =>
            Mathf.Lerp(backrestLow.y, backrestHigh.y, Mathf.InverseLerp(backrestLow.x, backrestHigh.x, y));

        private void PinFoot(AvatarIKGoal goal, Vector3 anchor)
        {
            _animator.SetIKPositionWeight(goal, feetPin);
            _animator.SetIKPosition(goal, anchor);
        }
    }
}
