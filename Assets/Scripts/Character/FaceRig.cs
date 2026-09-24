using System.Collections.Generic;
using UnityEngine;

namespace Interrogation.Character
{
    /// <summary>
    /// Név szerinti blendshape-írás az összes arc-mesh-en: ugyanaz a shape a testen, a szakállon és
    /// a szem-rétegeken is megvan, így egy írás mindegyiket együtt mozgatja.
    /// </summary>
    public sealed class FaceRig
    {
        private readonly Dictionary<string, List<(SkinnedMeshRenderer renderer, int index)>> _targets =
            new Dictionary<string, List<(SkinnedMeshRenderer, int)>>();

        public FaceRig(IEnumerable<SkinnedMeshRenderer> renderers)
        {
            foreach (var renderer in renderers)
            {
                var mesh = renderer.sharedMesh;
                if (mesh == null) continue;
                for (var i = 0; i < mesh.blendShapeCount; i++)
                {
                    var shape = mesh.GetBlendShapeName(i);
                    if (!_targets.TryGetValue(shape, out var list))
                        _targets[shape] = list = new List<(SkinnedMeshRenderer, int)>();
                    list.Add((renderer, i));
                }
            }
        }

        public bool Has(string shape) => _targets.ContainsKey(shape);

        /// <summary>0..1 skálán (a Unity 0..100-at vár).</summary>
        public void Set(string shape, float weight01)
        {
            if (!_targets.TryGetValue(shape, out var list)) return;
            var weight = Mathf.Clamp01(weight01) * 100f;
            foreach (var (renderer, index) in list)
                renderer.SetBlendShapeWeight(index, weight);
        }
    }
}
