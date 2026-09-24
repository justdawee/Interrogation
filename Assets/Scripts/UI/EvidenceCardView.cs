using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Interrogation.UI
{
    /// <summary>
    /// Bizonyíték-kártya az aktában. Ha a cáfolt tény már feltárult, elhalványul, de kijátszható marad.
    /// </summary>
    public sealed class EvidenceCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TMP_Text indexLabel;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Button confrontButton;
        [SerializeField] private CanvasGroup group;

        private Action _onConfront;
        private Action<bool> _onHover;

        public void Set(int index, string title, string description, Action onConfront, Action<bool> onHover)
        {
            if (indexLabel != null) indexLabel.text = index.ToString();
            if (titleLabel != null) titleLabel.text = title;
            if (descriptionLabel != null) descriptionLabel.text = description;
            _onConfront = onConfront;
            _onHover = onHover;
            if (confrontButton != null)
            {
                confrontButton.onClick.RemoveAllListeners();
                confrontButton.onClick.AddListener(() => _onConfront?.Invoke());
            }
            SetResolved(false);
        }

        public void SetResolved(bool resolved)
        {
            if (group != null) group.alpha = resolved ? 0.55f : 1f;
            if (statusLabel != null) statusLabel.text = resolved ? "ELLENTMONDÁS FELTÁRVA" : string.Empty;
        }

        public void SetLocked(bool locked)
        {
            if (confrontButton != null) confrontButton.interactable = !locked;
        }

        public void OnPointerEnter(PointerEventData eventData) => _onHover?.Invoke(true);

        public void OnPointerExit(PointerEventData eventData) => _onHover?.Invoke(false);
    }
}
