using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Interrogation.UI
{
    /// <summary>
    /// A kör-limit („SZALAG 14/20") és a türelem szegmentált sávja. Alacsony türelemnél vörös és lüktet,
    /// hogy az ügyvéd-vereség közeledte számok nélkül is érezhető legyen.
    /// </summary>
    public sealed class TapeStatusView : MonoBehaviour
    {
        [SerializeField] private TMP_Text tapeLabel;
        [SerializeField] private Image patienceFill;   // Filled, Horizontal
        [SerializeField] private TMP_Text patienceLabel;
        [SerializeField] private float lerpSpeed = 3f;
        [Range(0f, 1f)] [SerializeField] private float lowThreshold = 0.34f;
        [Tooltip("A felvillanás lecsengési ideje változáskor (mp).")]
        [SerializeField] private float flashSeconds = 0.9f;

        private float _target = 1f;
        private float _shown = 1f;
        private int _current = -1;
        private float _flash;

        public void SetTape(int round, int limit)
        {
            if (tapeLabel != null) tapeLabel.text = $"SZALAG {Mathf.Min(round, limit)}/{limit}";
        }

        /// <summary>A változás iránya: −1 fogyott, +1 nőtt, 0 változatlan vagy első beállítás.</summary>
        public int SetPatience(int current, int max, bool immediate = false)
        {
            var change = _current < 0 ? 0 : System.Math.Sign(current - _current);
            _current = current;
            _target = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            if (immediate) _shown = _target;
            if (change != 0) _flash = 1f;
            return change;
        }

        private void Update()
        {
            _shown = Mathf.MoveTowards(_shown, _target, lerpSpeed * Time.deltaTime);
            _flash = flashSeconds > 0f ? Mathf.MoveTowards(_flash, 0f, Time.deltaTime / flashSeconds) : 0f;
            if (patienceFill == null) return;

            patienceFill.fillAmount = _shown;
            var low = _target <= lowThreshold;
            var baseColor = low ? NoirPalette.StressHigh : NoirPalette.Amber;
            var pulse = low ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 3.5f)) : 1f;
            var color = Color.Lerp(baseColor, Color.white, _flash * 0.6f);
            patienceFill.color = new Color(color.r, color.g, color.b, Mathf.Max(pulse, _flash));

            if (patienceLabel == null) return;
            var labelColor = low ? NoirPalette.StressHigh : NoirPalette.TextMuted;
            patienceLabel.color = Color.Lerp(labelColor, NoirPalette.TextPrimary, _flash);
        }
    }
}
