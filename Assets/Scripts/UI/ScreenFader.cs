using System.Collections;
using UnityEngine;

namespace Interrogation.UI
{
    /// <summary>Teljes képernyős fekete áttűnés a jelenet újratöltéséhez. Időskálától független.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ScreenFader : MonoBehaviour
    {
        /// <summary>A következő betöltés feketéről indul.</summary>
        public static bool StartBlack;

        private CanvasGroup _group;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _group.alpha = StartBlack ? 1f : 0f;
            StartBlack = false;
        }

        public IEnumerator Fade(float to, float seconds)
        {
            if (_group == null) Awake();
            var from = _group.alpha;
            _group.blocksRaycasts = to > 0.5f;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                _group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            _group.alpha = to;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => StartBlack = false;
    }
}
