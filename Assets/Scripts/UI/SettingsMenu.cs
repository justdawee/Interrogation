using System;
using Interrogation.Settings;
using TMPro;

namespace Interrogation.UI
{
    /// <summary>
    /// A Beállítások lapozója (főmenü és szünet-menü közös). Az Esc az oldalról a hubra, a hubról a hívó
    /// menüjébe lép vissza.
    /// </summary>
    public sealed class SettingsMenu
    {
        private readonly MenuList _list;
        private readonly TMP_Text _title;
        private readonly Action _exit;
        private SettingsPage _page;
        private bool _confirmReset;

        /// <param name="title">Lehet null.</param>
        public SettingsMenu(MenuList list, TMP_Text title, Action exit)
        {
            _list = list;
            _title = title;
            _exit = exit ?? throw new ArgumentNullException(nameof(exit));
            if (_list != null) _list.Cancelled += Back;
        }

        public void Open() => Show(SettingsPage.Hub, 0);

        public void Back()
        {
            if (_page == SettingsPage.Hub) _exit();
            else Show(SettingsPage.Hub, (int)_page - 1);
        }

        private void Show(SettingsPage page, int selected)
        {
            if (_list == null) return;
            if (page != _page) UiSoundEvents.Raise(UiSound.Page);
            _page = page;
            _confirmReset = false;
            if (_title != null) _title.text = SettingsPages.Title(page);
            _list.Show(Build(page), selected);
        }

        private System.Collections.Generic.List<MenuEntry> Build(SettingsPage page)
        {
            switch (page)
            {
                case SettingsPage.Display:
                    DisplaySettings.BeginEdit();
                    return SettingsPages.Display(Back);
                case SettingsPage.Graphics: return SettingsPages.Graphics(Back);
                case SettingsPage.Effects: return SettingsPages.Effects(Back);
                case SettingsPage.Audio: return SettingsPages.Audio(Back);
                case SettingsPage.Gameplay: return SettingsPages.Gameplay(Back);
                default:
                    return SettingsPages.Hub(p => Show(p, 0),
                        () => _confirmReset ? "Biztosan visszaállítod?  <size=70%>(Enter)</size>" : "Alapértékek visszaállítása",
                        Reset, _exit);
            }
        }

        /// <summary>Kétlépéses: az első aktiválás megerősítést kér.</summary>
        private void Reset()
        {
            if (!_confirmReset)
            {
                _confirmReset = true;
                return;
            }
            _confirmReset = false;
            VideoSettings.ResetToDefaults();
            GameSettings.ResetToDefaults();
        }
    }
}
