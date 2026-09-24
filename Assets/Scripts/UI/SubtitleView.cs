using System;
using Interrogation.Llm;
using TMPro;
using UnityEngine;

namespace Interrogation.UI
{
    /// <summary>
    /// A felirat-sor írógép-animációval. Thinking alatt halvány „…" pulzál, ami a várakozást
    /// diegetikusan fedi el. A tempó egyezik a <c>SuspectLipSync</c>-kel.
    /// </summary>
    public sealed class SubtitleView : MonoBehaviour
    {
        [SerializeField] private RectTransform pill;      // a sötét, lekerekített háttér
        [SerializeField] private TMP_Text text;
        [SerializeField] private CanvasGroup group;

        [Tooltip("Írógép-tempó karakter/mp — a SuspectLipSync tempójával egyezzen.")]
        [SerializeField] private float charsPerSecond = 15f;
        [SerializeField] private float maxWidth = 1100f;
        [SerializeField] private Vector2 padding = new Vector2(28f, 14f);
        [Range(0f, 1f)] [SerializeField] private float emotionTint = 0.3f;

        private float _revealed;
        private int _total;
        private bool _thinking;

        public bool IsTyping => !_thinking && text != null && text.maxVisibleCharacters < _total;

        /// <summary>Teljes megjelenéskor vagy átugráskor sül el.</summary>
        public event Action LineFinished;

        private void Awake() => Clear();

        public void ShowLine(string line, SuspectEmotion emotion)
        {
            if (text == null) return;
            _thinking = false;
            text.color = Color.Lerp(NoirPalette.TextPrimary, NoirPalette.Emotion(emotion), emotionTint);
            SetContent($"„{line?.Trim()}\"");
            _revealed = 0f;
            text.maxVisibleCharacters = 0;
            SetAlpha(1f);
        }

        public void ShowThinking()
        {
            if (text == null) return;
            _thinking = true;
            text.color = NoirPalette.TextMuted;
            SetContent("…");
            text.maxVisibleCharacters = int.MaxValue;
        }

        public void Skip()
        {
            if (!IsTyping) return;
            text.maxVisibleCharacters = _total;
            LineFinished?.Invoke();
        }

        public void Clear()
        {
            _thinking = false;
            _total = 0;
            if (text != null) text.text = string.Empty;
            SetAlpha(0f);
        }

        private void Update()
        {
            if (text == null) return;

            if (_thinking)
            {
                SetAlpha(0.35f + 0.3f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f)));
                return;
            }

            if (text.maxVisibleCharacters >= _total) return;
            _revealed += charsPerSecond * GameSettings.SubtitleSpeed * Time.deltaTime;
            text.maxVisibleCharacters = Mathf.Min(_total, Mathf.FloorToInt(_revealed));
            if (text.maxVisibleCharacters >= _total) LineFinished?.Invoke();
        }

        private void SetContent(string content)
        {
            text.text = content;
            text.ForceMeshUpdate();
            _total = text.textInfo.characterCount;

            if (pill == null) return;
            var inner = maxWidth - padding.x * 2f;
            var size = text.GetPreferredValues(content, inner, 0f);
            pill.sizeDelta = new Vector2(Mathf.Min(size.x, inner) + padding.x * 2f, size.y + padding.y * 2f);
        }

        private void SetAlpha(float a)
        {
            if (group != null) group.alpha = a;
        }
    }
}
