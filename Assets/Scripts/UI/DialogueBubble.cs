using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Interrogation.UI
{
    public enum Speaker { Detective, Suspect }

    /// <summary>
    /// Egy transzkript-sor: a bal akcentus-sáv színe a beszélőt és az érzelmet jelzi.
    /// </summary>
    public sealed class DialogueBubble : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image accentBar;
        [SerializeField] private TMP_Text speakerLabel;
        [SerializeField] private TMP_Text messageText;

        public void Bind(Image bg, Image accent, TMP_Text label, TMP_Text message)
        {
            background = bg;
            accentBar = accent;
            speakerLabel = label;
            messageText = message;
        }

        public void Set(string speakerName, string message, Color accent, Color bg)
        {
            if (speakerLabel != null)
            {
                speakerLabel.text = speakerName;
                speakerLabel.color = accent;
            }
            if (messageText != null) messageText.text = message;
            if (accentBar != null) accentBar.color = accent;
            if (background != null) background.color = bg;
        }
    }
}
