using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Interrogation.Rendering
{
    /// <summary>
    /// Izzó körvonal az <see cref="OutlineTargets"/> rendererei köré. Két pass: a sziluett egy maszkba,
    /// majd a tágított maszk additív keverése a post-processing előtt, így a bloom is izzítja.
    /// </summary>
    public sealed class OutlineRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;
        [Tooltip("A körvonal szélessége pixelben, 1080p magasságra vetítve (felbontással skálázódik).")]
        [SerializeField] private float widthPixels = 5f;
        [Tooltip("A szín szorzója (HDR) — 1 fölött a bloom izzást ad a körvonalnak.")]
        [SerializeField] private float glow = 1.25f;
        [Tooltip("Halvány derengés a tárgy sziluettjén belül (0 = nincs).")]
        [Range(0f, 0.3f)] [SerializeField] private float fill = 0.05f;
        [SerializeField] private RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        private Material _material;
        private OutlinePass _pass;

        public override void Create()
        {
            if (shader == null) shader = Shader.Find("Hidden/Interrogation/HighlightOutline");
            if (shader == null) return;
            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new OutlinePass(_material) { renderPassEvent = passEvent };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || !OutlineTargets.IsVisible) return;
            if (renderingData.cameraData.cameraType != CameraType.Game) return;
            _pass.Configure(widthPixels, glow, fill);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(_material);

        private sealed class OutlinePass : ScriptableRenderPass
        {
            private const int MaskPass = 0;
            private const int CompositePass = 1;
            private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
            private static readonly int TexelId = Shader.PropertyToID("_OutlineTexel");
            private static readonly int FillId = Shader.PropertyToID("_OutlineFill");

            private readonly Material _material;
            private float _width, _glow, _fill;

            public OutlinePass(Material material)
            {
                _material = material;
                requiresIntermediateTexture = true;
            }

            public void Configure(float width, float glow, float fill)
            {
                _width = width;
                _glow = glow;
                _fill = fill;
            }

            private sealed class MaskData
            {
                internal Material material;
                internal Renderer[] renderers;
            }

            private sealed class CompositeData
            {
                internal Material material;
                internal TextureHandle mask;
                internal Vector4 texel;
                internal Color color;
                internal float fill;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                var desc = renderGraph.GetTextureDesc(resources.activeColorTexture);
                desc.name = "_HighlightOutlineMask";
                desc.format = GraphicsFormat.R8_UNorm;
                desc.msaaSamples = MSAASamples.None;
                desc.filterMode = FilterMode.Bilinear;
                desc.clearBuffer = true;
                desc.clearColor = Color.clear;
                var mask = renderGraph.CreateTexture(desc);

                using (var builder = renderGraph.AddRasterRenderPass<MaskData>("Highlight Outline Mask", out var data))
                {
                    data.material = _material;
                    data.renderers = CollectVisible();
                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (MaskData d, RasterGraphContext ctx) =>
                    {
                        foreach (var r in d.renderers)
                        {
                            var subMeshes = SubMeshCount(r);
                            for (int i = 0; i < subMeshes; i++) ctx.cmd.DrawRenderer(r, d.material, i, MaskPass);
                        }
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<CompositeData>("Highlight Outline Composite", out var data))
                {
                    var scale = desc.height / 1080f;
                    data.material = _material;
                    data.mask = mask;
                    data.texel = new Vector4(1f / desc.width, 1f / desc.height, Mathf.Max(1f, _width * scale), 0f);
                    data.color = OutlineTargets.Color * (_glow * OutlineTargets.Intensity);
                    data.fill = _fill;
                    builder.UseTexture(mask, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc(static (CompositeData d, RasterGraphContext ctx) =>
                    {
                        d.material.SetColor(ColorId, d.color);
                        d.material.SetVector(TexelId, d.texel);
                        d.material.SetFloat(FillId, d.fill);
                        Blitter.BlitTexture(ctx.cmd, d.mask, new Vector4(1f, 1f, 0f, 0f), d.material, CompositePass);
                    });
                }
            }

            private static Renderer[] CollectVisible()
            {
                var all = OutlineTargets.Renderers;
                var list = new System.Collections.Generic.List<Renderer>(all.Count);
                foreach (var r in all)
                    if (r != null && r.enabled && r.gameObject.activeInHierarchy) list.Add(r);
                return list.ToArray();
            }

            private static int SubMeshCount(Renderer r)
            {
                Mesh mesh = r is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                    : r.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
                return mesh != null ? Mathf.Max(1, mesh.subMeshCount) : 1;
            }
        }
    }
}
