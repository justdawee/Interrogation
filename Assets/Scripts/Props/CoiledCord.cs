using UnityEngine;

namespace Interrogation.Props
{
    /// <summary>
    /// Procedurális spirálkábel két pont között, amíg a kagyló mozog (az eredeti kábel merev modell).
    /// Lelógó Bézier-görbe köré tekert csavarvonal, amely nem süllyed az asztal síkja alá.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class CoiledCord : MonoBehaviour
    {
        [SerializeField] private Transform startAnchor;   // a készüléken
        [SerializeField] private Transform endAnchor;     // a kagylón
        [Tooltip("Merre lóg ki a kábel íve (világ-irány, pl. a készülék bal oldala).")]
        [SerializeField] private Vector3 slackDirection = new Vector3(0f, 0f, 1f);
        [SerializeField] private float slack = 0.09f;
        [SerializeField] private float sag = 0.06f;
        [SerializeField] private float deskHeight = 0.7762f; // az asztallap mért magassága
        [SerializeField] private float coilRadius = 0.004f;
        [SerializeField] private float turnsPerMeter = 160f;
        [SerializeField] private int samplesPerTurn = 10;
        [SerializeField] private int maxPoints = 2400;

        private LineRenderer _line;
        private Vector3[] _points = new Vector3[0];

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            if (_line == null) _line = GetComponent<LineRenderer>();
            _line.enabled = visible;
            if (visible) Rebuild();
        }

        private void LateUpdate()
        {
            if (_line != null && _line.enabled) Rebuild();
        }

        private void Rebuild()
        {
            if (startAnchor == null || endAnchor == null) return;
            var p0 = startAnchor.position;
            var p3 = endAnchor.position;
            var dir = slackDirection.normalized;
            var p1 = p0 + dir * slack + Vector3.down * sag;
            var p2 = p3 + dir * slack * 0.8f + Vector3.down * sag;

            var length = Approximate(p0, p1, p2, p3);
            var turns = Mathf.Max(1f, length * turnsPerMeter);
            var count = Mathf.Clamp(Mathf.CeilToInt(turns * samplesPerTurn), 16, maxPoints);
            if (_points.Length != count) _points = new Vector3[count];

            var floor = deskHeight + coilRadius;
            for (int i = 0; i < count; i++)
            {
                var s = i / (float)(count - 1);
                var c = Bezier(p0, p1, p2, p3, s);
                c.y = Mathf.Max(c.y, floor);
                var tangent = BezierTangent(p0, p1, p2, p3, s);
                var normal = Vector3.Cross(tangent, Vector3.up);
                if (normal.sqrMagnitude < 1e-6f) normal = Vector3.Cross(tangent, Vector3.right);
                normal.Normalize();
                var binormal = Vector3.Cross(tangent.normalized, normal);
                // A végeken a tekercs sugara elfogy, így a kábel simán fut be a csatlakozókba.
                var radius = coilRadius * Mathf.Clamp01(Mathf.Min(s, 1f - s) * 12f);
                var angle = s * turns * Mathf.PI * 2f;
                _points[i] = c + (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * radius;
            }
            _line.positionCount = count;
            _line.SetPositions(_points);
        }

        private static float Approximate(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            float len = 0f; var prev = a;
            for (int i = 1; i <= 16; i++)
            {
                var p = Bezier(a, b, c, d, i / 16f);
                len += Vector3.Distance(prev, p);
                prev = p;
            }
            return len;
        }

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            var u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        private static Vector3 BezierTangent(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            var u = 1f - t;
            return 3f * u * u * (b - a) + 6f * u * t * (c - b) + 3f * t * t * (d - c);
        }
    }
}
