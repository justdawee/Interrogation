using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Interrogation.UI
{
    public sealed class MenuEntry
    {
        public readonly Func<string> Label;
        public readonly Action Activate;
        public readonly Action<int> Adjust;
        public readonly Func<bool> Enabled;

        public MenuEntry(Func<string> label, Action activate, Action<int> adjust = null, Func<bool> enabled = null)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Activate = activate;
            Adjust = adjust;
            Enabled = enabled;
        }

        public MenuEntry(string label, Action activate, Func<bool> enabled = null)
            : this(() => label, activate, null, enabled) { }

        public bool IsEnabled => Enabled == null || Enabled();
    }

    /// <summary>
    /// Aktalapra gépelt menülista billentyűs és egeres vezérléssel, villogó írógép-kurzorral.
    /// Időskálától független, és ha több a tétel, mint a sor, görget.
    /// </summary>
    public sealed class MenuList : MonoBehaviour
    {
        [SerializeField] private TMP_Text[] rows = Array.Empty<TMP_Text>();
        [SerializeField] private float blinkPeriod = 0.9f;

        private readonly List<MenuEntry> _items = new List<MenuEntry>();
        private int _selected;
        private int _offset;
        private int _ignoreInputUntilFrame;
        private bool _renderPending;

        public event Action Cancelled;

        public bool Active { get; private set; }

        private void Awake()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null) continue;
                var index = i;
                var row = rows[i].GetComponent<MenuRow>();
                if (row == null) row = rows[i].gameObject.AddComponent<MenuRow>();
                // A rámutatás hangját a UiPointerHover adja.
                row.Hovered += () => { if (Active && Selectable(_offset + index)) _selected = _offset + index; };
                row.Clicked += () => { if (Active && _offset + index < _items.Count) { _selected = _offset + index; Activate(); } };
            }
        }

        /// <summary>Az aktiváló képkocka billentyűit figyelmen kívül hagyja.</summary>
        public void Show(IReadOnlyList<MenuEntry> items, int selected = 0)
        {
            _items.Clear();
            if (items != null) _items.AddRange(items);
            for (int i = 0; i < rows.Length; i++)
                if (rows[i] != null) rows[i].gameObject.SetActive(i < _items.Count);
            _offset = 0;
            _selected = Mathf.Clamp(selected, 0, Math.Max(0, _items.Count - 1));
            if (!Selectable(_selected)) Move(1);
            SetActive(true);
            Render();
        }

        /// <summary>A sorok láthatók maradnak, csak nem fogad bemenetet.</summary>
        public void SetActive(bool active)
        {
            Active = active;
            _ignoreInputUntilFrame = Time.frameCount + 1;
            _renderPending = true;
        }

        private void Update()
        {
            if (_items.Count == 0) return;
            // Inaktív lista csak egyszer rajzol, utána nincs képkockánkénti munka.
            if (!Active && !_renderPending) return;
            _renderPending = false;
            if (Active && Time.frameCount > _ignoreInputUntilFrame) HandleInput();
            Render();
        }

        private void HandleInput()
        {
            var scroll = UnityEngine.InputSystem.Mouse.current?.scroll.ReadValue().y ?? 0f;
            if (_items.Count > rows.Length && Mathf.Abs(scroll) > 0.01f)
            {
                Move(scroll > 0f ? -1 : 1);
                UiSoundEvents.Raise(UiSound.Hover);
            }

            var nav = GameInput.NavigatePressed;
            if (nav.y != 0)
            {
                var before = _selected;
                Move(-nav.y);
                if (_selected != before) UiSoundEvents.Raise(UiSound.Hover);
            }
            else if (nav.x != 0 && _items[_selected].Adjust != null)
            {
                _items[_selected].Adjust(nav.x);
                UiSoundEvents.Raise(UiSound.Click);
            }

            if (GameInput.Submit) Activate();
            else if (GameInput.Cancel)
            {
                UiSoundEvents.Raise(UiSound.Back);
                Cancelled?.Invoke();
            }
        }

        private void Activate()
        {
            if (_selected < 0 || _selected >= _items.Count) return;
            if (!_items[_selected].IsEnabled) { UiSoundEvents.Raise(UiSound.Error); return; }
            UiSoundEvents.Raise(UiSound.Click);
            var item = _items[_selected];
            // Beállító sornál az Enter körbe léptet.
            if (item.Activate != null) item.Activate();
            else item.Adjust?.Invoke(1);
        }

        private void Move(int step)
        {
            for (int n = 0; n < _items.Count; n++)
            {
                _selected = (_selected + step + _items.Count) % _items.Count;
                if (Selectable(_selected)) return;
            }
        }

        private bool Selectable(int i) => i >= 0 && i < _items.Count && _items[i].IsEnabled;

        private void KeepSelectionVisible()
        {
            if (_items.Count <= rows.Length) { _offset = 0; return; }
            if (_selected < _offset) _offset = _selected;
            else if (_selected >= _offset + rows.Length) _offset = _selected - rows.Length + 1;
        }

        private void Render()
        {
            KeepSelectionVisible();
            var cursorOn = Active && Mathf.Repeat(Time.unscaledTime, blinkPeriod) < blinkPeriod * 0.55f;
            for (int i = 0; i < rows.Length && _offset + i < _items.Count; i++)
            {
                var row = rows[i];
                if (row == null) continue;
                var item = _items[_offset + i];
                var selected = _offset + i == _selected;
                var cursor = selected && cursorOn ? "_" : "<alpha=#00>_";
                row.text = item.Label() + " " + cursor;
                row.color = !item.IsEnabled ? NoirPalette.InkMuted * new Color(1, 1, 1, 0.55f)
                    : selected ? NoirPalette.InkRed : NoirPalette.Ink;
            }
        }
    }
}
