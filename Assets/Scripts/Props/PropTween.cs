using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Interrogation.Props
{
    public readonly struct PropPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public PropPose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public static PropPose Of(Transform t) => new PropPose(t.position, t.rotation);

        public void ApplyTo(Transform t) => t.SetPositionAndRotation(Position, Rotation);
    }

    /// <summary>Kód-alapú tárgy-animációs segédek: simított időzítés, ívelt és többpontos mozgás.</summary>
    public static class PropTween
    {
        // smootherstep
        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        public static IEnumerator Run(float duration, Action<float> step)
        {
            if (duration <= 0f) { step(1f); yield break; }
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                step(Ease(t / duration));
                yield return null;
            }
            step(1f);
        }

        public static PropPose Lerp(PropPose a, PropPose b, float k, float arc = 0f)
        {
            var pos = Vector3.Lerp(a.Position, b.Position, k) + Vector3.up * (arc * Mathf.Sin(k * Mathf.PI));
            return new PropPose(pos, Quaternion.Slerp(a.Rotation, b.Rotation, k));
        }

        public static IEnumerator Move(Transform t, PropPose to, float duration, float arc = 0f)
        {
            var from = PropPose.Of(t);
            yield return Run(duration, k => Lerp(from, to, k, arc).ApplyTo(t));
        }

        /// <summary>
        /// Catmull–Rom út a pontokon át, húrhossz szerinti paraméterezéssel (egyenletes sebesség),
        /// így a tárgy megtorpanás nélkül kerülhet ki egy másikat.
        /// </summary>
        public static PropPose Along(IReadOnlyList<PropPose> path, float k)
        {
            int n = path.Count;
            if (n == 1) return path[0];
            float total = 0f;
            for (int i = 0; i < n - 1; i++) total += SegmentLength(path, i);

            float d = Mathf.Clamp01(k) * total;
            int s = 0;
            while (s < n - 2 && d > SegmentLength(path, s)) { d -= SegmentLength(path, s); s++; }
            float u = Mathf.Clamp01(d / SegmentLength(path, s));

            var pos = CatmullRom(path[Mathf.Max(s - 1, 0)].Position, path[s].Position,
                path[s + 1].Position, path[Mathf.Min(s + 2, n - 1)].Position, u);
            var rot = Quaternion.Slerp(path[s].Rotation, path[s + 1].Rotation, u * u * (3f - 2f * u));
            return new PropPose(pos, rot);
        }

        public static IEnumerator Follow(Transform t, IReadOnlyList<PropPose> path, float duration)
        {
            yield return Run(duration, k => Along(path, k).ApplyTo(t));
        }

        private static float SegmentLength(IReadOnlyList<PropPose> path, int i) =>
            Mathf.Max(1e-4f, Vector3.Distance(path[i].Position, path[i + 1].Position));

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        /// <summary>
        /// Az a gyökér-póz, amelynél a <paramref name="localPoint"/> a <paramref name="target"/> pontba kerül.
        /// </summary>
        public static PropPose RootPoseFor(Vector3 localPoint, Vector3 target, Quaternion rotation, Vector3 lossyScale) =>
            new PropPose(target - rotation * Vector3.Scale(lossyScale, localPoint), rotation);
    }

    /// <summary>
    /// Merev darabok együttes mozgatása egy virtuális forgáspont körül (pl. a kagyló darabjai,
    /// amelyeket a prefab-szerkezet miatt nem lehet egy szülő alá tenni).
    /// </summary>
    public sealed class RigidGroup
    {
        private readonly List<Transform> _parts = new List<Transform>();
        private readonly List<PropPose> _relative = new List<PropPose>();
        public PropPose Pivot { get; private set; }

        public void Capture(IEnumerable<Transform> parts, PropPose pivot)
        {
            _parts.Clear();
            _relative.Clear();
            Pivot = pivot;
            var inv = Quaternion.Inverse(pivot.Rotation);
            foreach (var p in parts)
            {
                if (p == null) continue;
                _parts.Add(p);
                _relative.Add(new PropPose(inv * (p.position - pivot.Position), inv * p.rotation));
            }
        }

        public void Apply(PropPose pivot)
        {
            Pivot = pivot;
            for (int i = 0; i < _parts.Count; i++)
            {
                var rel = _relative[i];
                _parts[i].SetPositionAndRotation(pivot.Position + pivot.Rotation * rel.Position, pivot.Rotation * rel.Rotation);
            }
        }

        public IEnumerator Move(PropPose to, float duration, float arc = 0f)
        {
            var from = Pivot;
            yield return PropTween.Run(duration, k => Apply(PropTween.Lerp(from, to, k, arc)));
        }

        public IEnumerator Follow(IReadOnlyList<PropPose> path, float duration)
        {
            yield return PropTween.Run(duration, k => Apply(PropTween.Along(path, k)));
        }
    }
}
