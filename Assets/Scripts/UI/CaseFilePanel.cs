using System;
using System.Collections.Generic;
using Interrogation.Memory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Interrogation.UI
{
    /// <summary>
    /// Az akta: oldalról becsúszó panel három füllel (Bizonyítékok, Nyomok, Jegyzőkönyv). A még
    /// ismeretlen nyomok „???" sorként látszanak.
    /// </summary>
    public sealed class CaseFilePanel : MonoBehaviour
    {
        public enum Tab { Evidence = 0, Clues = 1, Transcript = 2 }

        [SerializeField] private RectTransform slider;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private float slideSpeed = 9f;

        [Header("Fülek")]
        [SerializeField] private Button[] tabButtons = new Button[3];
        [SerializeField] private GameObject[] pages = new GameObject[3];

        [Header("Tartalom")]
        [Tooltip("Az ügy címe és a gyanúsított neve — a CaseData-ból töltődik.")]
        [SerializeField] private TMP_Text headerLabel;
        [SerializeField] private EvidenceCardView cardTemplate;
        [SerializeField] private TMP_Text clueTemplate;
        [SerializeField] private DialogueUI transcript;

        private readonly List<EvidenceCardView> _cards = new List<EvidenceCardView>();
        private readonly List<TMP_Text> _clueRows = new List<TMP_Text>();
        private IReadOnlyList<ContradictionPair> _evidence = Array.Empty<ContradictionPair>();
        private IReadOnlyList<FactNode> _facts = Array.Empty<FactNode>();
        private float _openT;      // 0 = zárva, 1 = nyitva
        private Tab _tab;

        public bool IsOpen { get; private set; }

        /// <summary>Amíg az előző szembesítés tart, új bizonyíték nem választható.</summary>
        public bool SelectionLocked { get; private set; }

        /// <summary>Az egér alatti kártya fact_id-ja, vagy null.</summary>
        public string HoveredEvidenceId { get; private set; }
        public DialogueUI Transcript => transcript;

        public event Action<ContradictionPair> EvidenceChosen;

        public event Action<bool> OpenChanged;

        private void Awake()
        {
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
            if (clueTemplate != null) clueTemplate.gameObject.SetActive(false);
            for (int i = 0; i < tabButtons.Length; i++)
            {
                var tab = (Tab)i;
                if (tabButtons[i] != null) tabButtons[i].onClick.AddListener(() => ShowTab(tab));
            }
            ShowTab(Tab.Evidence);
            ApplySlide();
        }

        public void Populate(CaseData caseData)
        {
            _evidence = caseData != null ? caseData.contradictions : (IReadOnlyList<ContradictionPair>)Array.Empty<ContradictionPair>();
            _facts = caseData != null ? caseData.facts : (IReadOnlyList<FactNode>)Array.Empty<FactNode>();
            if (headerLabel != null)
                headerLabel.text = caseData != null ? $"„{caseData.caseTitle}\" — {caseData.suspectName}" : string.Empty;

            Rebuild(_cards, cardTemplate, _evidence.Count);
            for (int i = 0; i < _evidence.Count; i++)
            {
                var e = _evidence[i];
                _cards[i].Set(i + 1, Capitalize(e.evidenceLabel), e.description, () => Choose(e), over => OnCardHover(e, over));
                _cards[i].SetLocked(SelectionLocked);
            }

            Rebuild(_clueRows, clueTemplate, _facts.Count);
            RefreshClues(null);
        }

        public void RefreshClues(IReadOnlyCollection<string> revealed)
        {
            bool Has(string id) => revealed != null && id != null && Contains(revealed, id);

            for (int i = 0; i < _facts.Count; i++)
            {
                var f = _facts[i];
                var known = Has(f.id);
                _clueRows[i].text = known ? $"— {ClueText(f)}" : $"— ???  <alpha=#88>({f.role})";
                _clueRows[i].color = known ? NoirPalette.TextPrimary : NoirPalette.TextMuted;
            }
            for (int i = 0; i < _evidence.Count; i++)
                _cards[i].SetResolved(Has(_evidence[i].factId));
        }

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (IsOpen == open) return;
            IsOpen = open;
            UiSoundEvents.Raise(open ? UiSound.Open : UiSound.Close);
            if (!open) HoveredEvidenceId = null;
            if (group != null) { group.interactable = open; group.blocksRaycasts = open; }
            OpenChanged?.Invoke(open);
        }

        public void ShowTab(Tab tab)
        {
            if (IsOpen && tab != _tab) UiSoundEvents.Raise(UiSound.Page);
            _tab = tab;
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] != null) pages[i].SetActive(i == (int)tab);
                if (tabButtons[i] != null && tabButtons[i].targetGraphic != null)
                    tabButtons[i].targetGraphic.color = i == (int)tab ? NoirPalette.Amber : NoirPalette.PanelSoft;
                var label = tabButtons[i] != null ? tabButtons[i].GetComponentInChildren<TMP_Text>() : null;
                if (label != null) label.color = i == (int)tab ? NoirPalette.Background : NoirPalette.TextPrimary;
            }
        }

        /// <summary>1-alapú gyorsbillentyű (1–3).</summary>
        public void ChooseByIndex(int oneBased)
        {
            if (!IsOpen || _tab != Tab.Evidence) return;
            var i = oneBased - 1;
            if (i >= 0 && i < _evidence.Count) Choose(_evidence[i]);
        }

        public bool ChooseById(string factId)
        {
            foreach (var e in _evidence)
            {
                if (e == null || !string.Equals(e.factId, factId, StringComparison.OrdinalIgnoreCase)) continue;
                return Choose(e);
            }
            return false;
        }

        public void SetSelectionLocked(bool locked)
        {
            SelectionLocked = locked;
            foreach (var card in _cards) card.SetLocked(locked);
        }

        public static string ClueText(FactNode f) =>
            !string.IsNullOrWhiteSpace(f.clueLabel) ? f.clueLabel : Capitalize(f.role);

        private bool Choose(ContradictionPair evidence)
        {
            if (SelectionLocked) return false;
            EvidenceChosen?.Invoke(evidence);
            SetOpen(false);
            return true;
        }

        private void OnCardHover(ContradictionPair evidence, bool over)
        {
            if (over) HoveredEvidenceId = evidence.factId;
            else if (string.Equals(HoveredEvidenceId, evidence.factId, StringComparison.OrdinalIgnoreCase)) HoveredEvidenceId = null;
        }

        private void Update()
        {
            var target = IsOpen ? 1f : 0f;
            if (Mathf.Approximately(_openT, target)) return;
            _openT = Mathf.MoveTowards(_openT, target, slideSpeed * Time.unscaledDeltaTime);
            ApplySlide();
        }

        private void ApplySlide()
        {
            var k = Mathf.SmoothStep(0f, 1f, _openT);
            if (group != null) group.alpha = k;
            if (slider != null)
                slider.anchoredPosition = new Vector2(Mathf.Lerp(-slider.rect.width - 40f, 0f, k), slider.anchoredPosition.y);
        }

        private static void Rebuild<T>(List<T> items, T template, int count) where T : Component
        {
            foreach (var item in items) if (item != null) Destroy(item.gameObject);
            items.Clear();
            if (template == null) return;
            for (int i = 0; i < count; i++)
            {
                var clone = Instantiate(template, template.transform.parent);
                clone.gameObject.SetActive(true);
                items.Add(clone);
            }
        }

        private static bool Contains(IReadOnlyCollection<string> set, string id)
        {
            foreach (var s in set)
                if (string.Equals(s, id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? string.Empty : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
