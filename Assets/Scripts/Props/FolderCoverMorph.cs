using System.Collections;
using UnityEngine;

namespace Interrogation.Props
{
    /// <summary>
    /// A mappa nyitás-animációja 210 egymást követő blendshape-ként érkezett (Alembic-export). A tört
    /// képkocka két szomszédos cél keveréke, így a nyitás tetszőleges sebességgel és visszafelé is megy.
    /// </summary>
    public sealed class FolderCoverMorph
    {
        private readonly SkinnedMeshRenderer _renderer;
        private readonly int _count;
        private int _a = -1, _b = -1;

        public FolderCoverMorph(SkinnedMeshRenderer renderer)
        {
            _renderer = renderer;
            _count = renderer != null && renderer.sharedMesh != null ? renderer.sharedMesh.blendShapeCount : 0;
            if (_renderer != null) _renderer.updateWhenOffscreen = true; // a nyitott fedél kilóg az import-bounds-ból
        }

        public float Frame { get; private set; }

        public void SetFrame(float frame)
        {
            if (_count == 0) return;
            Frame = Mathf.Clamp(frame, 0f, _count - 1);
            var a = Mathf.FloorToInt(Frame);
            var b = Mathf.Min(a + 1, _count - 1);
            var t = Frame - a;

            if (_a >= 0) _renderer.SetBlendShapeWeight(_a, 0f);
            if (_b >= 0) _renderer.SetBlendShapeWeight(_b, 0f);
            _renderer.SetBlendShapeWeight(a, 100f * (1f - t));
            if (b != a) _renderer.SetBlendShapeWeight(b, 100f * t);
            _a = a; _b = b;
        }

        public void Rest()
        {
            if (_a >= 0) _renderer.SetBlendShapeWeight(_a, 0f);
            if (_b >= 0) _renderer.SetBlendShapeWeight(_b, 0f);
            _a = _b = -1;
            Frame = 0f;
        }

        public IEnumerator Play(float from, float to, float seconds)
        {
            yield return PropTween.Run(seconds, k => SetFrame(Mathf.Lerp(from, to, k)));
        }
    }
}
