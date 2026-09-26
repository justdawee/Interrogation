using System.Collections.Generic;
using UnityEngine;

namespace Interrogation.Rendering
{
    /// <summary>A kiemelő körvonal aktuális célpontja, színe és erőssége (egyszerre egy csoport).</summary>
    public static class OutlineTargets
    {
        private static readonly List<Renderer> _renderers = new List<Renderer>();

        public static IReadOnlyList<Renderer> Renderers => _renderers;
        public static Color Color { get; private set; } = Color.white;
        public static float Intensity { get; private set; }

        public static bool IsVisible => Intensity > 0.001f && _renderers.Count > 0;

        public static void Set(IReadOnlyList<Renderer> renderers, Color color, float intensity)
        {
            _renderers.Clear();
            if (renderers != null)
                foreach (var r in renderers)
                    if (r != null) _renderers.Add(r);
            Color = color;
            Intensity = Mathf.Clamp01(intensity);
        }

        public static void Clear()
        {
            _renderers.Clear();
            Intensity = 0f;
        }
    }
}
