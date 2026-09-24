using Interrogation.Llm;
using UnityEngine;

namespace Interrogation.Character
{
    /// <summary>
    /// Légzés, ideges testsúly-áthelyezés és izzadás a stressz szerint.
    /// A tekintet előtt fut, hogy a fej a légzés-mozgást kompenzálhassa.
    /// </summary>
    [DefaultExecutionOrder(0)]
    public sealed class SuspectVitals : MonoBehaviour
    {
        [SerializeField] private SuspectMood mood;
        [SerializeField] private Animator animator;

        [Header("Légzés")]
        [SerializeField] private Vector2 breathsPerMinute = new Vector2(13f, 30f);
        [SerializeField] private Vector2 breathAngle = new Vector2(1.2f, 2.8f);
        [Tooltip("A mellkas-csontok helyi oldal-tengelye (kalibrált: +X).")]
        [SerializeField] private Vector3 chestLocalRight = Vector3.right;

        [Header("Izzadás")]
        [Tooltip("A bőr-anyag simaság-felső értéke (Reallusion bőr-shader).")]
        [SerializeField] private string smoothnessProperty = "_SmoothnessMax";
        [SerializeField] private float maxSweatBoost = 0.22f;
        [Tooltip("Ennyi stressz alatt nincs izzadás.")]
        [Range(0f, 1f)] [SerializeField] private float sweatFromStress = 0.35f;

        private Transform _chest, _upperChest, _spine;
        private float _phase;
        private float _breathRate;
        private float _sweat;
        private float _appliedSweat = -1f;
        private SkinnedMeshRenderer _body;
        private MaterialPropertyBlock _block;
        private float[] _baseSmoothness;

        private void Awake()
        {
            if (mood == null) mood = GetComponent<SuspectMood>();
            if (animator == null) animator = GetComponent<Animator>();
            _spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            _chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            _upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            _breathRate = breathsPerMinute.x;
            CacheSkin();
        }

        private void CacheSkin()
        {
            var bodyTransform = transform.Find("CC_Base_Body");
            _body = bodyTransform != null ? bodyTransform.GetComponent<SkinnedMeshRenderer>() : null;
            if (_body == null) return;
            var materials = _body.sharedMaterials;
            _baseSmoothness = new float[materials.Length];
            for (var i = 0; i < materials.Length; i++)
                _baseSmoothness[i] = materials[i] != null && materials[i].HasProperty(smoothnessProperty)
                    ? materials[i].GetFloat(smoothnessProperty) : float.NaN;
            _block = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            var stress = mood != null ? mood.Stress01 : 0f;
            var emotion = mood != null ? mood.Emotion : SuspectEmotion.Calm;
            var agitated = emotion == SuspectEmotion.Hostile || emotion == SuspectEmotion.Breaking;

            var targetRate = Mathf.Lerp(breathsPerMinute.x, breathsPerMinute.y, stress) + (agitated ? 5f : 0f);
            _breathRate = Mathf.Lerp(_breathRate, targetRate, 1f - Mathf.Exp(-0.8f * Time.deltaTime));
            _phase += Time.deltaTime * _breathRate / 60f * Mathf.PI * 2f;
            var angle = Mathf.Sin(_phase) * Mathf.Lerp(breathAngle.x, breathAngle.y, stress);
            if (_chest != null) _chest.localRotation *= Quaternion.AngleAxis(angle * 0.6f, chestLocalRight);
            if (_upperChest != null) _upperChest.localRotation *= Quaternion.AngleAxis(angle * 0.4f, chestLocalRight);

            if (_spine != null)
            {
                var sway = (Mathf.PerlinNoise(Time.time * 0.35f, 7.1f) - 0.5f) * 2f * Mathf.Lerp(0.4f, 2.2f, stress);
                _spine.rotation = Quaternion.AngleAxis(sway, transform.forward) * _spine.rotation;
            }

            UpdateSweat(stress);
        }

        private void UpdateSweat(float stress)
        {
            if (_body == null) return;
            var goal = Mathf.InverseLerp(sweatFromStress, 1f, stress);
            _sweat = Mathf.MoveTowards(_sweat, goal, Time.deltaTime * 0.08f);
            if (Mathf.Abs(_sweat - _appliedSweat) < 0.005f) return;
            _appliedSweat = _sweat;
            for (var i = 0; i < _baseSmoothness.Length; i++)
            {
                if (float.IsNaN(_baseSmoothness[i])) continue;
                _body.GetPropertyBlock(_block, i);
                _block.SetFloat(smoothnessProperty, Mathf.Clamp01(_baseSmoothness[i] + _sweat * maxSweatBoost));
                _body.SetPropertyBlock(_block, i);
            }
        }
    }
}
