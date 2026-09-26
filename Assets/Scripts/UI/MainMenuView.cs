using System;
using System.Text;
using Interrogation.Memory;
using TMPro;
using UnityEngine;

namespace Interrogation.UI
{
    /// <summary>
    /// A diegetikus főmenü: aktafedlap, majd az eligazítás. Csak eseményt ad, a fázisokat a
    /// <c>GameDirector</c> vezérli.
    /// </summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        [Header("Lapok")]
        [SerializeField] private FolderPanel cover;
        [SerializeField] private FolderPanel briefing;
        [SerializeField] private FolderPanel settings;

        [Header("Listák")]
        [SerializeField] private MenuList coverList;
        [SerializeField] private MenuList briefingList;
        [SerializeField] private MenuList settingsList;

        [Header("Tartalom")]
        [SerializeField] private TMP_Text coverTitle;
        [SerializeField] private TMP_Text coverSubject;
        [SerializeField] private TMP_Text briefingBody;

        public event Action NewCaseRequested;
        public event Action BeginRequested;
        public event Action BackRequested;

        private SettingsMenu _settingsMenu;

        private void Awake()
        {
            if (briefingList != null) briefingList.Cancelled += () => BackRequested?.Invoke();
            _settingsMenu = new SettingsMenu(settingsList, FolderTitle(settings), ShowCover);
            HideAll(immediate: true);
        }

        public void Populate(CaseData caseData)
        {
            if (caseData == null) return;
            if (coverTitle != null) coverTitle.text = $"„{caseData.caseTitle}\"";
            if (coverSubject != null) coverSubject.text = $"Gyanúsított: {caseData.suspectName}\nÜgyszám: {caseData.caseId.ToUpperInvariant()}";
            if (briefingBody != null) briefingBody.text = BriefingText(caseData);
        }

        public void ShowCover()
        {
            Show(cover);
            coverList?.Show(new[]
            {
                new MenuEntry("Új kihallgatás", () => NewCaseRequested?.Invoke()),
                new MenuEntry("Beállítások", ShowSettings),
                new MenuEntry("Kilépés", Quit),
            });
        }

        public void ShowBriefing()
        {
            UiSoundEvents.Raise(UiSound.Open);
            Show(briefing);
            briefingList?.Show(new[]
            {
                new MenuEntry("Kihallgatás megkezdése", () => BeginRequested?.Invoke()),
                new MenuEntry("Vissza", () => BackRequested?.Invoke()),
            });
        }

        public void Hide()
        {
            UiSoundEvents.Raise(UiSound.Close);
            HideAll(immediate: false);
        }

        private void ShowSettings()
        {
            UiSoundEvents.Raise(UiSound.Page);
            Show(settings);
            _settingsMenu.Open();
        }

        internal static TMP_Text FolderTitle(FolderPanel folder) =>
            folder != null ? folder.transform.Find("Title")?.GetComponent<TMP_Text>() : null;

        private void Show(FolderPanel page)
        {
            foreach (var p in new[] { cover, briefing, settings })
                if (p != null) p.SetShown(p == page);
            foreach (var l in new[] { coverList, briefingList, settingsList })
                l?.SetActive(false);
        }

        private void HideAll(bool immediate)
        {
            foreach (var p in new[] { cover, briefing, settings })
                if (p != null) p.SetShown(false, immediate);
            foreach (var l in new[] { coverList, briefingList, settingsList })
                l?.SetActive(false);
        }

        private static string BriefingText(CaseData c)
        {
            var sb = new StringBuilder();
            sb.AppendLine(c.briefing);
            sb.AppendLine($"<b>Gyanúsított:</b> {c.suspectName}");
            sb.AppendLine();
            sb.AppendLine("<b>Bizonyítékok az aktában</b>");
            for (int i = 0; i < c.contradictions.Count; i++)
            {
                var e = c.contradictions[i];
                if (e == null) continue;
                sb.AppendLine($"{i + 1}. {Capitalize(e.evidenceLabel)} — <i>{e.description}</i>");
            }
            sb.AppendLine();
            sb.AppendLine("<b>Cél:</b> csikard ki a vallomást, mielőtt lejár a szalag, vagy ügyvédet kér.");
            sb.AppendLine($"<size=85%>Kérdezz szabadon. Szembesítsd a bizonyítékkal (Tab: akta, 1–3). " +
                          $"A bizonyíték nélküli vád fogyasztja a türelmét. Szalag: {c.roundLimit} kérdés.</size>");
            return sb.ToString();
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? string.Empty : char.ToUpperInvariant(s[0]) + s.Substring(1);

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
