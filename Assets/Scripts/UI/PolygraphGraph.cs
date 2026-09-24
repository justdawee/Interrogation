using UnityEngine;
using UnityEngine.UI;

namespace Interrogation.UI
{
    /// <summary>
    /// Élő poligráf-görbe: az alap-kilengés a stresszel nő, változáskor kitér, ellentmondásnál tüskét ad.
    /// A mesh-t maga építi, textúra nélkül.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PolygraphGraph : MaskableGraphic
    {
        [SerializeField] private int sampleCount = 90;
        [SerializeField] private float samplesPerSecond = 22f;
        [SerializeField] private float thickness = 2.2f;
        [SerializeField] private float baseAmplitude = 0.06f;
        [SerializeField] private float stressAmplitude = 0.42f;
        [SerializeField] private float kickDecay = 2.8f;
        [SerializeField] private float colorLerpSpeed = 2f;

        private float[] _samples;
        private int _head;
        private float _accumulator;
        private float _stress;     // 0..1
        private float _kick;       // a friss kitérés ereje, lecseng
        private float _kickSign = 1f;

        public void SetStress(float normalized) => _stress = Mathf.Clamp01(normalized);

        /// <summary>Egyszeri kitérés (0..1).</summary>
        public void Kick(float strength)
        {
            _kick = Mathf.Max(_kick, Mathf.Clamp01(strength));
            _kickSign = -_kickSign;
        }

        protected override void Awake()
        {
            base.Awake();
            EnsureBuffer();
        }

        private void EnsureBuffer()
        {
            if (_samples != null && _samples.Length == sampleCount) return;
            _samples = new float[Mathf.Max(8, sampleCount)];
            _head = 0;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            EnsureBuffer();

            var target = NoirPalette.Stress(_stress);
            color = Color.Lerp(color, target, colorLerpSpeed * Time.deltaTime);

            _accumulator += Time.deltaTime * samplesPerSecond;
            var added = false;
            while (_accumulator >= 1f)
            {
                _accumulator -= 1f;
                Push(NextSample());
                added = true;
            }
            _kick = Mathf.MoveTowards(_kick, 0f, kickDecay * Time.deltaTime);
            if (added) SetVerticesDirty();
        }

        private float NextSample()
        {
            var t = Time.time;
            var amp = baseAmplitude + stressAmplitude * _stress;
            // szívverés-szerű alapjel, a stresszel gyorsul
            var rate = 1.2f + 2.2f * _stress;
            var wave = Mathf.Sin(t * rate * 6.283f) * 0.6f + (Mathf.PerlinNoise(t * 3.1f, 0.37f) - 0.5f) * 0.9f;
            var spike = _kick * _kickSign * (Random.value * 0.4f + 0.6f);
            _kickSign = -_kickSign;
            return Mathf.Clamp(wave * amp + spike, -1f, 1f);
        }

        private void Push(float value)
        {
            _samples[_head] = value;
            _head = (_head + 1) % _samples.Length;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            EnsureBuffer();

            var rect = GetPixelAdjustedRect();
            var n = _samples.Length;
            var step = rect.width / (n - 1);
            var halfH = rect.height * 0.5f;
            var midY = rect.center.y;

            var prev = Point(0);
            for (int i = 1; i < n; i++)
            {
                var cur = Point(i);
                AddSegment(vh, prev, cur);
                prev = cur;
            }

            Vector2 Point(int i)
            {
                var v = _samples[(_head + i) % n]; // a legrégebbi balra
                return new Vector2(rect.xMin + i * step, midY + v * halfH);
            }
        }

        private void AddSegment(VertexHelper vh, Vector2 a, Vector2 b)
        {
            var dir = (b - a).normalized;
            var normal = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);
            var idx = vh.currentVertCount;
            var c = (Color32)color;
            vh.AddVert(a - normal, c, Vector2.zero);
            vh.AddVert(a + normal, c, Vector2.zero);
            vh.AddVert(b + normal, c, Vector2.zero);
            vh.AddVert(b - normal, c, Vector2.zero);
            vh.AddTriangle(idx, idx + 1, idx + 2);
            vh.AddTriangle(idx + 2, idx + 3, idx);
        }
    }
}
