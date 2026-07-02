using UnityEngine;

namespace Interrogation.Rendering
{
    /// <summary>
    /// A nagyító shader példányonkénti paraméterezése MaterialPropertyBlock-kal, így több lencse
    /// osztozhat egy anyagon. A lencse közepe kézi, a mesh-ből számolt vagy egy transformot követő lehet.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MagnifyingGlassController : MonoBehaviour
    {
        public enum CenterMode { Manual, AutoFromMesh, Transform }

        const string ShaderName = "Custom/MagnifyingGlass";

        [Header("Lens Center")]
        [Tooltip("How _CenterOS is resolved for this lens.")]
        public CenterMode centerMode = CenterMode.Manual;
        [Tooltip("Center in object space (used when Center Mode = Manual).")]
        public Vector3 manualCenterOS = Vector3.zero;
        [Tooltip("World transform to follow (used when Center Mode = Transform). Can be an empty GameObject.")]
        public Transform centerTransform;

        [Header("Magnification")]
        [Range(1f, 4f)]  public float zoom = 1.8f;
        [Range(0f, 2f)]  public float edgeCurvature = 0.35f;

        [Header("Glass Look")]
        [Tooltip("Enable to override the material's appearance per-instance. If off, only the lens center is applied.")]
        public bool overrideAppearance = true;
        [ColorUsage(true, false)] public Color glassTint = new Color(0.86f, 0.93f, 0.96f, 1f);
        [Range(0f, 0.02f)] public float chromaticAberration = 0.0035f;

        [Header("Rim and Glint")]
        [ColorUsage(true, false)] public Color rimColor = Color.white;
        [Range(0.5f, 8f)] public float rimPower = 3f;
        [Range(0f, 2f)]   public float rimStrength = 0.55f;
        [Range(0f, 4f)]   public float specular = 1.4f;
        [Range(0f, 1f)]   public float smoothness = 0.92f;

        static readonly int IdCenter     = Shader.PropertyToID("_CenterOS");
        static readonly int IdZoom       = Shader.PropertyToID("_Zoom");
        static readonly int IdBarrel     = Shader.PropertyToID("_Barrel");
        static readonly int IdGlassTint  = Shader.PropertyToID("_GlassTint");
        static readonly int IdChromatic  = Shader.PropertyToID("_Chromatic");
        static readonly int IdRimColor   = Shader.PropertyToID("_RimColor");
        static readonly int IdRimPower   = Shader.PropertyToID("_RimPower");
        static readonly int IdRimStrength= Shader.PropertyToID("_RimStrength");
        static readonly int IdSpecular   = Shader.PropertyToID("_Specular");
        static readonly int IdSmoothness = Shader.PropertyToID("_Smoothness");

        Renderer _renderer;
        MaterialPropertyBlock _block;
        Mesh _cachedMesh;
        int _cachedSlot = -1;
        Vector3 _cachedCentroid;

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        void LateUpdate()
        {
            if (centerMode == CenterMode.Transform && centerTransform != null)
                Apply();
        }

        [ContextMenu("Apply Now")]
        public void Apply()
        {
            if (_renderer == null) _renderer = GetComponent<Renderer>();
            if (_renderer == null) return;

            int slot = FindLensSlot(_renderer.sharedMaterials);
            if (slot < 0) return;

            _block ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block, slot);

            _block.SetVector(IdCenter, ResolveCenterOS(slot));

            if (overrideAppearance)
            {
                _block.SetFloat(IdZoom, zoom);
                _block.SetFloat(IdBarrel, edgeCurvature);
                _block.SetColor(IdGlassTint, glassTint);
                _block.SetFloat(IdChromatic, chromaticAberration);
                _block.SetColor(IdRimColor, rimColor);
                _block.SetFloat(IdRimPower, rimPower);
                _block.SetFloat(IdRimStrength, rimStrength);
                _block.SetFloat(IdSpecular, specular);
                _block.SetFloat(IdSmoothness, smoothness);
            }

            _renderer.SetPropertyBlock(_block, slot);
        }

        Vector4 ResolveCenterOS(int slot)
        {
            switch (centerMode)
            {
                case CenterMode.Transform when centerTransform != null:
                    Vector3 local = _renderer.transform.InverseTransformPoint(centerTransform.position);
                    return new Vector4(local.x, local.y, local.z, 0f);

                case CenterMode.AutoFromMesh:
                    return GetCentroidOS(slot);

                default:
                    return new Vector4(manualCenterOS.x, manualCenterOS.y, manualCenterOS.z, 0f);
            }
        }

        Vector4 GetCentroidOS(int slot)
        {
            var mf = GetComponent<MeshFilter>();
            var mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null) return Vector4.zero;

            int sub = Mathf.Min(slot, mesh.subMeshCount - 1);
            if (mesh == _cachedMesh && slot == _cachedSlot)
                return new Vector4(_cachedCentroid.x, _cachedCentroid.y, _cachedCentroid.z, 0f);

            int[] tris = mesh.GetTriangles(sub);
            if (tris.Length == 0) return Vector4.zero;
            Vector3[] verts = mesh.vertices;
            var seen = new System.Collections.Generic.HashSet<int>();
            Vector3 sum = Vector3.zero;
            foreach (int i in tris) if (seen.Add(i)) sum += verts[i];
            _cachedCentroid = seen.Count > 0 ? sum / seen.Count : Vector3.zero;
            _cachedMesh = mesh;
            _cachedSlot = slot;
            return new Vector4(_cachedCentroid.x, _cachedCentroid.y, _cachedCentroid.z, 0f);
        }

        static int FindLensSlot(Material[] mats)
        {
            if (mats == null) return -1;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].shader != null && mats[i].shader.name == ShaderName)
                    return i;
            return -1;
        }
    }
}
