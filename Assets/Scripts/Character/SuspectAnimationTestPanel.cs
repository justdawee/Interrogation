using Interrogation.Llm;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Interrogation.Character
{
    /// <summary>
    /// Teszt-panel (F1) a karakter animációihoz és állapotaihoz LLM-hívás nélkül.
    /// Csak az Editorban és fejlesztői buildben aktív.
    /// </summary>
    public sealed class SuspectAnimationTestPanel : MonoBehaviour
    {
        private static readonly string[] EmotionLabels = { "Nyugodt", "Ideges", "Védekező", "Ellenséges", "Összeomló" };
        private static readonly string[] StateNames = { "Calm", "Nervous", "Defensive", "Hostile", "Breaking", "Caught", "Outburst", "SmugLaugh" };

        [SerializeField] private SuspectCharacterBinder binder;
        [SerializeField] private SuspectMood mood;
        [SerializeField] private SuspectLipSync lipSync;
        [SerializeField] private Animator animator;
        [SerializeField] private bool visibleOnStart = true;

        [TextArea] [SerializeField] private string shortLine = "Nem tudom, miről beszél, felügyelő.";
        [TextArea] [SerializeField] private string longLine =
            "Egész este otthon voltam, egyedül. Ezt már elmondtam a kollégáinak is, többször. " +
            "Reinhardttal jóban voltunk, soha nem ártottam volna neki. Keressék inkább azt, aki betört!";

        private bool _visible;
        private float _stress;
        private bool _thinking;
        private GUIStyle _box, _header, _button, _selected, _label;

        private void Awake()
        {
            if (!Debug.isDebugBuild) { enabled = false; return; }
            if (binder == null) binder = GetComponent<SuspectCharacterBinder>();
            if (mood == null) mood = GetComponent<SuspectMood>();
            if (lipSync == null) lipSync = GetComponent<SuspectLipSync>();
            if (animator == null) animator = GetComponent<Animator>();
            _visible = visibleOnStart;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible || binder == null || mood == null) return;
            EnsureStyles();
            var scale = Mathf.Max(1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            GUILayout.BeginArea(new Rect(16, 16, 300, 640), _box);
            GUILayout.Label("Animáció-teszt  (F1: elrejtés)", _header);
            GUILayout.Label($"Állapot: {CurrentState()}   Beszéd-réteg: {TalkWeight():0.00}", _label);
            GUILayout.Label(binder.PreviewActive ? "Vezérlés: TESZT (előnézet)" : "Vezérlés: játék", _label);

            GUILayout.Space(6);
            GUILayout.Label("Érzelem", _header);
            for (var i = 0; i < EmotionLabels.Length; i++)
            {
                var active = binder.PreviewActive && mood.Emotion == (SuspectEmotion)i;
                if (GUILayout.Button(EmotionLabels[i], active ? _selected : _button))
                    binder.SetPreview((SuspectEmotion)i, _stress, _thinking);
            }

            GUILayout.Space(6);
            GUILayout.Label($"Stressz: {_stress:0.00}", _header);
            var stress = GUILayout.HorizontalSlider(_stress, 0f, 1f);
            if (!Mathf.Approximately(stress, _stress)) { _stress = stress; Reapply(); }
            GUILayout.Space(8);
            var thinking = GUILayout.Toggle(_thinking, " Gondolkodik (Thinking)");
            if (thinking != _thinking) { _thinking = thinking; Reapply(); }

            GUILayout.Space(6);
            GUILayout.Label("Reakciók (egyszeri)", _header);
            if (GUILayout.Button("Rajtakapva", _button)) mood.RequestReaction(SuspectReaction.Caught);
            if (GUILayout.Button("Dühkitörés", _button)) mood.RequestReaction(SuspectReaction.Outburst);
            if (GUILayout.Button("Kaján nevetés", _button)) mood.RequestReaction(SuspectReaction.SmugLaugh);

            GUILayout.Space(6);
            GUILayout.Label("Beszéd", _header);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Rövid mondat", _button)) lipSync?.Speak(shortLine);
            if (GUILayout.Button("Hosszú mondat", _button)) lipSync?.Speak(longLine);
            if (GUILayout.Button("Csend", _button)) lipSync?.Stop();
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            if (GUILayout.Button("Teszt vége — a játék vezérel", _button)) binder.ClearPreview();
            GUILayout.EndArea();
        }

        private void Reapply()
        {
            if (binder.PreviewActive) binder.SetPreview(mood.Emotion, _stress, _thinking);
        }

        private string CurrentState()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return "-";
            var info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            foreach (var name in StateNames)
                if (info.IsName(name)) return animator.IsInTransition(0) ? "→ " + name : name;
            return "?";
        }

        private float TalkWeight()
        {
            if (animator == null) return 0f;
            var layer = animator.GetLayerIndex("Talking");
            return layer >= 0 ? animator.GetLayerWeight(layer) : 0f;
        }

        private void EnsureStyles()
        {
            if (_box != null) return;
            var bg = new Texture2D(1, 1);
            bg.SetPixel(0, 0, new Color(0.06f, 0.06f, 0.07f, 0.88f));
            bg.Apply();
            _box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 10, 12) };
            _box.normal.background = bg;
            _header = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
            _header.normal.textColor = new Color(0.95f, 0.78f, 0.45f);
            _label = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _label.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            _button = new GUIStyle(GUI.skin.button) { fontSize = 12, fixedHeight = 24 };
            _selected = new GUIStyle(_button) { fontStyle = FontStyle.Bold };
            _selected.normal.textColor = new Color(0.95f, 0.78f, 0.45f);
        }
    }
}
