using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Interrogation.UI
{
    public sealed class MenuRow : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public event Action Hovered;
        public event Action Clicked;

        public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke();
        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();
    }
}
