using System;
using System.Collections.Generic;
using UnityEngine;

namespace Interrogation.Character
{
    /// <summary>
    /// Hang nélküli ajakmozgás: a szövegből betűnként visémát és állkapocs-nyitást számol, és a
    /// felirat tempójában játssza le.
    /// </summary>
    public sealed class SuspectLipSync : MonoBehaviour
    {
        public static readonly string[] Visemes =
            { "Open", "Wide", "Tight-O", "Tight", "Explosive", "Dental_Lip", "Lip_Open", "Affricate" };

        [Tooltip("Beszéd-tempó karakter/másodpercben (a felirat írógép-tempójával érdemes egyeztetni).")]
        [SerializeField] private float charsPerSecond = 15f;

        [Tooltip("A száj-formák közti átmenet sebessége (nagyobb = pattogósabb).")]
        [SerializeField] private float blendSpeed = 18f;

        private readonly List<Segment> _timeline = new List<Segment>();
        private readonly float[] _weights = new float[Visemes.Length];
        private float _startTime;
        private float _duration;
        private int _cursor;

        public bool IsSpeaking { get; private set; }

        public float JawOpen01 { get; private set; }

        /// <summary>A <see cref="Visemes"/> sorrendjében.</summary>
        public IReadOnlyList<float> Weights => _weights;

        public event Action Finished;

        /// <summary>Visszaadja a becsült időtartamot másodpercben.</summary>
        public float Speak(string text, float? tempo = null)
        {
            _timeline.Clear();
            _cursor = 0;
            var cps = Mathf.Max(4f, (tempo ?? charsPerSecond) * UI.GameSettings.SubtitleSpeed);
            _duration = 0f;
            if (!string.IsNullOrWhiteSpace(text))
            {
                foreach (var c in text.ToLowerInvariant())
                {
                    var seg = Classify(c, 1f / cps);
                    _duration += seg.Duration;
                    seg.End = _duration;
                    _timeline.Add(seg);
                }
            }
            _startTime = Time.time;
            IsSpeaking = _timeline.Count > 0;
            return _duration;
        }

        public void Stop()
        {
            _timeline.Clear();
            IsSpeaking = false;
        }

        private void Update()
        {
            var targetViseme = -1;
            var targetJaw = 0f;
            if (IsSpeaking)
            {
                var t = Time.time - _startTime;
                while (_cursor < _timeline.Count && _timeline[_cursor].End < t) _cursor++;
                if (_cursor >= _timeline.Count)
                {
                    IsSpeaking = false;
                    Finished?.Invoke();
                }
                else
                {
                    targetViseme = _timeline[_cursor].Viseme;
                    targetJaw = _timeline[_cursor].Jaw;
                }
            }

            var k = 1f - Mathf.Exp(-blendSpeed * Time.deltaTime);
            for (var i = 0; i < _weights.Length; i++)
                _weights[i] = Mathf.Lerp(_weights[i], i == targetViseme ? 1f : 0f, k);
            JawOpen01 = Mathf.Lerp(JawOpen01, targetJaw, k);
        }

        private static Segment Classify(char c, float unit)
        {
            switch (c)
            {
                case 'a': case 'á': return new Segment(0, 0.65f, unit);
                case 'e': case 'é': return new Segment(1, 0.4f, unit);
                case 'i': case 'í': return new Segment(1, 0.2f, unit);
                case 'o': case 'ó': case 'ö': case 'ő': return new Segment(2, 0.45f, unit);
                case 'u': case 'ú': case 'ü': case 'ű': return new Segment(2, 0.25f, unit);
                case 'm': case 'b': case 'p': return new Segment(4, 0f, unit);
                case 'f': case 'v': return new Segment(5, 0.08f, unit);
                case 's': case 'z': case 'c': case 'j': return new Segment(3, 0.12f, unit);
                case 't': case 'd': case 'n': case 'l': case 'r': case 'k': case 'g': case 'h': case 'y':
                    return new Segment(6, 0.2f, unit);
                case ' ': return new Segment(-1, 0f, unit * 0.6f);
                case ',': case ';': case ':': case '-': case '–': return new Segment(-1, 0f, unit * 4f);
                case '.': case '!': case '?': case '…': return new Segment(-1, 0f, unit * 7f);
                default: return char.IsLetter(c) ? new Segment(7, 0.15f, unit) : new Segment(-1, 0f, unit * 0.5f);
            }
        }

        private struct Segment
        {
            public readonly int Viseme;
            public readonly float Jaw;
            public readonly float Duration;
            public float End;

            public Segment(int viseme, float jaw, float duration)
            {
                Viseme = viseme;
                Jaw = jaw;
                Duration = duration;
                End = 0f;
            }
        }
    }
}
