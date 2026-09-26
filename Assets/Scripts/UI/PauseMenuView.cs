using System;
using UnityEngine;

namespace Interrogation.UI
{
    /// <summary>
    /// A szünet-menü. A „Feladás" csak függő LLM-kör nélkül választható. Csak eseményt ad,
    /// a szünetet a <c>PauseController</c> kezeli.
    /// </summary>
    public sealed class PauseMenuView : MonoBehaviour
    {
        [Tooltip("A szoba elsötétítése a szünet-akta mögött.")]
        [SerializeField] private FolderPanel backdrop;
        [SerializeField] private FolderPanel main;
        [SerializeField] private FolderPanel settings;
        [SerializeField] private MenuList mainList;
        [SerializeField] private MenuList settingsList;

        public event Action ResumeRequested;
        public event Action GiveUpRequested;
        public event Action MainMenuRequested;

        private Func<bool> _canGiveUp = () => true;
        private SettingsMenu _settingsMenu;

        private void Awake()
        {
            if (mainList != null) mainList.Cancelled += () => ResumeRequested?.Invoke();
            _settingsMenu = new SettingsMenu(settingsList, MainMenuView.FolderTitle(settings), ShowMain);
            Hide(immediate: true);
        }

        public void Show(Func<bool> canGiveUp)
        {
            _canGiveUp = canGiveUp ?? (() => true);
            backdrop?.SetShown(true);
            ShowMain();
        }

        public void Hide(bool immediate = false)
        {
            backdrop?.SetShown(false, immediate);
            main?.SetShown(false, immediate);
            settings?.SetShown(false, immediate);
            mainList?.SetActive(false);
            settingsList?.SetActive(false);
        }

        private void ShowMain()
        {
            settings?.SetShown(false);
            settingsList?.SetActive(false);
            main?.SetShown(true);
            mainList?.Show(new[]
            {
                new MenuEntry("Folytatás", () => ResumeRequested?.Invoke()),
                new MenuEntry("Beállítások", ShowSettings),
                new MenuEntry(() => _canGiveUp() ? "Feladás" : "Feladás  <size=70%>(várj a válaszra)</size>",
                    () => GiveUpRequested?.Invoke(), null, () => _canGiveUp()),
                new MenuEntry("Főmenü", () => MainMenuRequested?.Invoke()),
            });
        }

        private void ShowSettings()
        {
            main?.SetShown(false);
            mainList?.SetActive(false);
            settings?.SetShown(true);
            _settingsMenu.Open();
        }
    }
}
