using System.Collections;
using Interrogation.Llm;
using UnityEngine;
using UnityEngine.UI;

namespace Interrogation.UI
{
    /// <summary>A görgethető kihallgatás-transzkript; új sornál az aljára görget.</summary>
    public sealed class DialogueUI : MonoBehaviour
    {
        [Tooltip("A ScrollRect, amelynek a Content-jébe a sorok kerülnek.")]
        [SerializeField] private ScrollRect scrollRect;

        [Tooltip("Egy inaktív minta-sor (DialogueBubble), amit klónozunk minden üzenethez.")]
        [SerializeField] private DialogueBubble bubbleTemplate;

        [SerializeField] private string detectiveName = "DETEKTÍV";
        [SerializeField] private string suspectName = "GYANÚSÍTOTT";

        private RectTransform _content;

        private void Awake()
        {
            if (scrollRect != null) _content = scrollRect.content;
            if (bubbleTemplate != null) bubbleTemplate.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (scrollRect != null) StartCoroutine(ScrollToBottomNextFrame());
        }

        public void Configure(string suspectDisplayName)
        {
            if (!string.IsNullOrWhiteSpace(suspectDisplayName))
                suspectName = suspectDisplayName.ToUpperInvariant();
        }

        public void AddDetectiveLine(string message)
        {
            Spawn(detectiveName, message, NoirPalette.ColdBlue, NoirPalette.DetectiveBg);
        }

        public void AddSuspectLine(string message, SuspectEmotion emotion)
        {
            Spawn(suspectName, message, NoirPalette.Emotion(emotion), NoirPalette.SuspectBg);
        }

        private void Spawn(string speaker, string message, Color accent, Color bg)
        {
            // Lusta init: az akta inaktív fülén az Awake még nem futott.
            if (_content == null && scrollRect != null) _content = scrollRect.content;
            if (bubbleTemplate == null || _content == null) return;

            var bubble = Instantiate(bubbleTemplate, _content);
            bubble.gameObject.SetActive(true);
            bubble.Set(speaker, message, accent, bg);

            if (isActiveAndEnabled) StartCoroutine(ScrollToBottomNextFrame());
        }

        private IEnumerator ScrollToBottomNextFrame()
        {
            // Egy képkocka, hogy a layout újraszámoljon.
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
