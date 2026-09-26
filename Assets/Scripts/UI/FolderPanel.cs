using UnityEngine;

namespace Interrogation.UI
{
    /// <summary>
    /// Aktalap megjelenítése áttűnéssel és rövid „asztalra csúsztatással". Időskálától független,
    /// így a szünet-menü is animál.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class FolderPanel : MonoBehaviour
    {
        [SerializeField] private float speed = 4.5f;
        [SerializeField] private Vector2 hiddenOffset = new Vector2(-60f, -40f);
        [SerializeField] private float hiddenTilt = -4f;

        private CanvasGroup _group;
        private RectTransform _rt;
        private Vector2 _shownPos;
        private Quaternion _shownRot;
        private float _t;

        public bool IsShown { get; private set; }

        private void Awake() => Init();

        private void Init()
        {
            if (_group != null) return;
            _group = GetComponent<CanvasGroup>();
            _rt = (RectTransform)transform;
            _shownPos = _rt.anchoredPosition;
            _shownRot = _rt.localRotation;
            Apply();
        }

        public void SetShown(bool shown, bool immediate = false)
        {
            Init();
            IsShown = shown;
            _group.interactable = shown;
            _group.blocksRaycasts = shown;
            if (immediate) { _t = shown ? 1f : 0f; Apply(); }
        }

        private void Update()
        {
            var target = IsShown ? 1f : 0f;
            if (Mathf.Approximately(_t, target)) return;
            _t = Mathf.MoveTowards(_t, target, speed * Time.unscaledDeltaTime);
            Apply();
        }

        private void Apply()
        {
            var k = Mathf.SmoothStep(0f, 1f, _t);
            _group.alpha = k;
            _rt.anchoredPosition = _shownPos + hiddenOffset * (1f - k);
            _rt.localRotation = _shownRot * Quaternion.Euler(0f, 0f, hiddenTilt * (1f - k));
        }
    }
}
