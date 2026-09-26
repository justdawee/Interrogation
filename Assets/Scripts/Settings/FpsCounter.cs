using UnityEngine;

namespace Interrogation.Settings
{
    /// <summary>Képkocka-számláló; fél másodpercenként építi újra a szöveget, így nem szemetel.</summary>
    public sealed class FpsCounter : MonoBehaviour
    {
        private const float Interval = 0.5f;

        private readonly GUIContent _content = new GUIContent();
        private GUIStyle _style;
        private int _frames;
        private float _elapsed;

        private void OnEnable()
        {
            _frames = 0;
            _elapsed = 0f;
            _content.text = "— FPS";
        }

        private void Update()
        {
            _frames++;
            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < Interval) return;
            var fps = _frames / _elapsed;
            _content.text = $"{fps:0} FPS  ·  {1000f / fps:0.0} ms";
            _frames = 0;
            _elapsed = 0f;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, Screen.height / 60), fontStyle = FontStyle.Bold };
            var size = _style.CalcSize(_content);
            var rect = new Rect(12, 8, size.x, size.y);
            _style.normal.textColor = new Color(0, 0, 0, 0.8f);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), _content, _style);
            _style.normal.textColor = new Color(0.95f, 0.85f, 0.6f);
            GUI.Label(rect, _content, _style);
        }
    }
}
