using System;
using UnityEngine;

namespace Interrogation.Settings
{
    /// <summary>Felsorolt játékos-beállítás <see cref="PlayerPrefs"/>-ben, körbe léptethető.</summary>
    public sealed class OptionSetting
    {
        private const string Prefix = "Interrogation.Video.";

        private readonly string _key;
        private readonly int _default;
        private readonly Action _changed;
        private int _value = int.MinValue;

        public OptionSetting(string key, int defaultIndex, Action changed, params string[] labels)
        {
            if (labels == null || labels.Length == 0) throw new ArgumentException("Legalább egy címke kell.", nameof(labels));
            _key = Prefix + key;
            Labels = labels;
            _default = Mathf.Clamp(defaultIndex, 0, labels.Length - 1);
            _changed = changed;
        }

        public string[] Labels { get; }
        public int Count => Labels.Length;
        public int Default => _default;
        public string Label => Labels[Value];
        public bool On => Value != 0;

        public int Value
        {
            get
            {
                if (_value == int.MinValue) _value = Mathf.Clamp(PlayerPrefs.GetInt(_key, _default), 0, Count - 1);
                return _value;
            }
            set
            {
                var clamped = Mathf.Clamp(value, 0, Count - 1);
                if (clamped == Value) return;
                _value = clamped;
                PlayerPrefs.SetInt(_key, clamped);
                _changed?.Invoke();
            }
        }

        public void Step(int delta) => Value = ((Value + delta) % Count + Count) % Count;

        /// <summary>Esemény nélkül; a hívó a végén egyszer jelez.</summary>
        public void ResetSilently()
        {
            _value = _default;
            PlayerPrefs.DeleteKey(_key);
        }

        public void SetSilently(int value)
        {
            _value = Mathf.Clamp(value, 0, Count - 1);
            PlayerPrefs.SetInt(_key, _value);
        }
    }
}
