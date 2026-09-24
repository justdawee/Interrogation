using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Interrogation.UI
{
    /// <summary>
    /// A kérdés-sor: bármely betű vagy Enter fókuszálja, Enterre küld. A csatolt bizonyítékot chip jelzi;
    /// ilyenkor kísérőszöveg nélkül is küldhető. Thinking alatt tiltott.
    /// </summary>
    public sealed class QuestionBar : MonoBehaviour
    {
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Button evidenceChip;
        [SerializeField] private TMP_Text evidenceLabel;
        [SerializeField] private float disabledAlpha = 0.25f;
        [SerializeField] private float fadeSpeed = 6f;

        private string _evidenceId;
        private bool _interactable = true;
        private bool _evidenceLocked;
        private bool _moveCaretToEnd;
        private int _lastLength;
        private bool _silentEdit;

        /// <summary>(kérdés, csatolt bizonyíték id-ja vagy null)</summary>
        public event Action<string, string> Submitted;

        public bool TypeToFocus { get; set; } = true;

        /// <summary>Csatolás vagy levétel (null); küldéskor nem sül el.</summary>
        public event Action<string> EvidenceChanged;

        public bool IsInteractable => _interactable;
        public bool IsFocused => inputField != null && inputField.isFocused;
        public string AttachedEvidenceId => _evidenceId;
        public bool HasText => inputField != null && !string.IsNullOrEmpty(inputField.text);

        private void Awake()
        {
            if (inputField != null)
            {
                inputField.onSubmit.AddListener(_ => Submit());
                inputField.onValueChanged.AddListener(OnValueChanged);
                // Az Esc a szünet-menüé — ne dobja el a félig begépelt kérdést.
                inputField.restoreOriginalTextOnEscape = false;
                // A Tab az akta gyorsbillentyűje — ne kerüljön a kérdésbe.
                inputField.onValidateInput = (_, __, c) => c == '\t' ? '\0' : c;
            }
            if (evidenceChip != null) evidenceChip.onClick.AddListener(ClearEvidence);
            SetEvidence(null, string.Empty, notify: false);
        }

        private void OnEnable()
        {
            if (Keyboard.current != null) Keyboard.current.onTextInput += OnTextInput;
        }

        private void OnDisable()
        {
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
        }

        public void SetInteractable(bool value)
        {
            _interactable = value;
            if (inputField != null) inputField.interactable = value;
            RefreshChip();
            if (group != null) group.blocksRaycasts = value;
            if (!value && inputField != null) inputField.DeactivateInputField();
        }

        public void SetEvidenceLocked(bool locked)
        {
            _evidenceLocked = locked;
            RefreshChip();
        }

        private void RefreshChip()
        {
            if (evidenceChip != null) evidenceChip.interactable = _interactable && !_evidenceLocked;
        }

        public void Focus()
        {
            if (!_interactable || inputField == null) return;
            inputField.ActivateInputField();
            _moveCaretToEnd = true;
        }

        public void Blur()
        {
            if (inputField != null) inputField.DeactivateInputField();
        }

        public void AttachEvidence(string evidenceId, string label)
        {
            if (!_interactable || _evidenceLocked) return;
            SetEvidence(evidenceId, label, notify: true);
        }

        public void ClearEvidence()
        {
            if (_evidenceLocked) return;
            SetEvidence(null, string.Empty, notify: true);
        }

        private void SetEvidence(string evidenceId, string label, bool notify)
        {
            var changed = !string.Equals(_evidenceId, evidenceId, StringComparison.Ordinal);
            _evidenceId = evidenceId;
            if (evidenceLabel != null) evidenceLabel.text = label;
            if (evidenceChip != null) evidenceChip.gameObject.SetActive(!string.IsNullOrEmpty(evidenceId));
            if (!notify || !changed) return;
            UiSoundEvents.Raise(UiSound.Page);
            EvidenceChanged?.Invoke(evidenceId);
        }

            // A program általi ürítés néma.
        private void OnValueChanged(string value)
        {
            var length = value?.Length ?? 0;
            if (!_silentEdit && length != _lastLength) UiSoundEvents.Raise(UiSound.Type);
            _lastLength = length;
        }

        private void Submit()
        {
            if (!_interactable || inputField == null) return;
            var question = inputField.text?.Trim() ?? string.Empty;
            if (question.Length == 0 && _evidenceId == null) return;

            var evidence = _evidenceId;
            _silentEdit = true;
            inputField.text = string.Empty;
            _silentEdit = false;
            UiSoundEvents.Raise(UiSound.Send);
            // A chip eltűnik, de a bizonyíték a válasz végéig „kint marad".
            SetEvidence(null, string.Empty, notify: false);
            Submitted?.Invoke(question, evidence);
        }

        private void Update()
        {
            if (group != null)
            {
                var target = _interactable ? 1f : disabledAlpha;
                group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
            }

            if (TypeToFocus && !IsFocused && GameInput.Focus)
                Focus();
        }

        private void LateUpdate()
        {
            if (!_moveCaretToEnd || !IsFocused) return;
            inputField.MoveTextEnd(false);
            _moveCaretToEnd = false;
        }

        private void OnTextInput(char c)
        {
            if (!TypeToFocus || !_interactable || IsFocused || inputField == null) return;
            if (!char.IsLetterOrDigit(c) && !char.IsPunctuation(c)) return;
            inputField.text += c;
            Focus();
        }
    }
}
