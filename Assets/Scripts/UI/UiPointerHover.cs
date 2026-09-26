using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Interrogation.UI
{
    /// <summary>
    /// Rámutatás-hang az egész felületre: ha az egér új, használható kattintható elemre ér, jelzést ad,
    /// így nem kell minden elemre külön hangot kötni.
    /// </summary>
    public sealed class UiPointerHover : MonoBehaviour
    {
        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private PointerEventData _pointer;
        private GameObject _current;
        private Vector2 _lastPosition = new Vector2(float.NaN, float.NaN);

        private void Update()
        {
            var mouse = Mouse.current;
            var events = EventSystem.current;
            if (mouse == null || events == null) return;

            var position = mouse.position.ReadValue();
            if (position == _lastPosition) return; // a billentyűs választás külön szól
            _lastPosition = position;

            var target = ClickableUnder(events, position);
            if (target == _current) return;
            _current = target;
            if (target != null) UiSoundEvents.Raise(UiSound.Hover);
        }

        private GameObject ClickableUnder(EventSystem events, Vector2 position)
        {
            if (_pointer == null) _pointer = new PointerEventData(events);
            _pointer.position = position;
            _hits.Clear();
            events.RaycastAll(_pointer, _hits);
            if (_hits.Count == 0) return null;

            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(_hits[0].gameObject);
            return handler != null && IsUsable(handler) ? handler : null;
        }

        // Aktív, és sem a gomb, sem egy szülő CanvasGroup nem tiltja.
        private static bool IsUsable(GameObject go)
        {
            var selectable = go.GetComponent<Selectable>();
            if (selectable != null) return selectable.IsInteractable();
            for (var t = go.transform; t != null; t = t.parent)
            {
                var group = t.GetComponent<CanvasGroup>();
                if (group == null) continue;
                if (!group.interactable || group.alpha <= 0.01f) return false;
                if (group.ignoreParentGroups) break;
            }
            return true;
        }
    }
}
