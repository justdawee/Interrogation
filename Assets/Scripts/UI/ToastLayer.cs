using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Interrogation.UI
{
    /// <summary>
    /// Rövid visszajelzések egymás után: „Új nyom: …" és a bélyegzőszerűen lecsapódó „ELLENTMONDÁS".
    /// </summary>
    public sealed class ToastLayer : MonoBehaviour
    {
        [Header("Nyom-toast")]
        [SerializeField] private RectTransform toast;
        [SerializeField] private CanvasGroup toastGroup;
        [SerializeField] private TMP_Text toastText;
        [SerializeField] private Vector2 toastPadding = new Vector2(22f, 10f);

        [Header("Pecsét")]
        [SerializeField] private RectTransform stamp;
        [SerializeField] private CanvasGroup stampGroup;
        [SerializeField] private TMP_Text stampText;

        [Header("Időzítés (mp)")]
        [SerializeField] private float fade = 0.25f;
        [SerializeField] private float toastHold = 2f;
        [SerializeField] private float stampHold = 1.4f;

        private readonly Queue<IEnumerator> _queue = new Queue<IEnumerator>();
        private bool _running;

        /// <summary>A pecsét lecsapódásának pillanata (a hang ide időzít).</summary>
        public event System.Action<string> StampShown;
        public event System.Action ToastShown;

        private void Awake()
        {
            if (toastGroup != null) toastGroup.alpha = 0f;
            if (stampGroup != null) stampGroup.alpha = 0f;
        }

        public void ShowClue(string clue) => Enqueue(ToastRoutine($"Új nyom: {clue}"));

        public void ShowStamp(string label) => Enqueue(StampRoutine(label));

        public void ShowToast(string message) => Enqueue(ToastRoutine(message));

        private void Enqueue(IEnumerator routine)
        {
            _queue.Enqueue(routine);
            if (!_running && isActiveAndEnabled) StartCoroutine(Pump());
        }

        private IEnumerator Pump()
        {
            _running = true;
            while (_queue.Count > 0) yield return _queue.Dequeue();
            _running = false;
        }

        private IEnumerator ToastRoutine(string message)
        {
            if (toast == null || toastText == null) yield break;
            toastText.text = message;
            var size = toastText.GetPreferredValues(message, 1200f, 0f);
            toast.sizeDelta = size + toastPadding * 2f;
            ToastShown?.Invoke();

            yield return Fade(toastGroup, 0f, 1f, fade, toast, 0.92f);
            yield return new WaitForSeconds(toastHold);
            yield return Fade(toastGroup, 1f, 0f, fade, toast, 1f);
        }

        private IEnumerator StampRoutine(string label)
        {
            if (stamp == null) yield break;
            if (stampText != null) stampText.text = label;
            yield return Fade(stampGroup, 0f, 1f, 0.12f, stamp, 1.7f);
            StampShown?.Invoke(label);
            yield return new WaitForSeconds(stampHold);
            yield return Fade(stampGroup, 1f, 0f, fade * 1.6f, stamp, 1f);
        }

        private static IEnumerator Fade(CanvasGroup group, float from, float to, float duration, RectTransform scaled, float scaleFrom)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / duration);
                if (group != null) group.alpha = Mathf.Lerp(from, to, k);
                if (scaled != null) scaled.localScale = Vector3.one * Mathf.Lerp(scaleFrom, 1f, k);
                yield return null;
            }
            if (group != null) group.alpha = to;
            if (scaled != null) scaled.localScale = Vector3.one;
        }
    }
}
