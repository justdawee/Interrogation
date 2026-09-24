using UnityEngine;

namespace Interrogation.Character
{
    /// <summary>
    /// Animator Controller nélkül minden képkocka elején visszaállítja a mentett alap-pózt, különben a
    /// procedurális rétegek eltolásai összeadódnának. Controllerrel magától hatástalan.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class SuspectPoseBase : MonoBehaviour
    {
        [Tooltip("A csontváz gyökere (CC_Base_BoneRoot). Üresen az Animator Hips-csontjának szülőjét használja.")]
        [SerializeField] private Transform skeletonRoot;

        private Animator _animator;
        private Transform[] _bones;
        private Quaternion[] _rotations;
        private Vector3[] _positions;

        public Animator Animator => _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (skeletonRoot == null && _animator != null && _animator.isHuman)
                skeletonRoot = _animator.GetBoneTransform(HumanBodyBones.Hips).parent;

            _bones = (skeletonRoot != null ? skeletonRoot : transform).GetComponentsInChildren<Transform>(true);
            _rotations = new Quaternion[_bones.Length];
            _positions = new Vector3[_bones.Length];
            for (var i = 0; i < _bones.Length; i++)
            {
                _rotations[i] = _bones[i].localRotation;
                _positions[i] = _bones[i].localPosition;
            }
        }

        private bool IsAnimatorDriving =>
            _animator != null && _animator.enabled && _animator.runtimeAnimatorController != null;

        private void LateUpdate()
        {
            if (IsAnimatorDriving) return;
            for (var i = 0; i < _bones.Length; i++)
            {
                _bones[i].localRotation = _rotations[i];
                _bones[i].localPosition = _positions[i];
            }
        }
    }
}
