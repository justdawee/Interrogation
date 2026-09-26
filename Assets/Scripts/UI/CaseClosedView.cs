using System;
using System.Collections.Generic;
using System.Text;
using Interrogation.Game;
using Interrogation.Memory;
using TMPro;
using UnityEngine;

namespace Interrogation.UI
{
    /// <summary>
    /// A „Lezárt akta" összegzés a szoba felett: kimenet-pecsét, körök, ellentmondások, a felfedett és
    /// a ki nem derített tények, valamint az A–D értékelés tételesen. A pontszámot a <see cref="CaseGrader"/> adja.
    /// </summary>
    public sealed class CaseClosedView : MonoBehaviour
    {
        [SerializeField] private FolderPanel backdrop;
        [SerializeField] private FolderPanel panel;
        [SerializeField] private MenuList actions;

        [Header("Szövegek")]
        [SerializeField] private TMP_Text stamp;
        [SerializeField] private TMP_Text header;
        [SerializeField] private TMP_Text findings;
        [SerializeField] private TMP_Text gradeLetter;
        [SerializeField] private TMP_Text gradeScore;
        [SerializeField] private TMP_Text gradeBreakdown;

        public event Action RetryRequested;
        public event Action MainMenuRequested;

        private void Awake()
        {
            backdrop?.SetShown(false, immediate: true);
            panel?.SetShown(false, immediate: true);
            actions?.SetActive(false);
        }

        public void Show(CaseData caseData, InterrogationOutcome outcome, int rounds,
            IReadOnlyCollection<string> revealed, CaseGradeReport report)
        {
            if (caseData == null || report == null) return;

            if (stamp != null)
            {
                stamp.text = InterrogationHUD.EndingStamp(outcome);
                stamp.color = outcome.IsVictory() ? NoirPalette.InkRed : NoirPalette.InkMuted;
            }
            if (header != null)
                header.text = $"„{caseData.caseTitle}\" — {caseData.suspectName}\n<size=80%>{OutcomeLine(outcome, caseData.suspectName)}</size>";
            if (findings != null) findings.text = Findings(caseData, rounds, revealed, report);
            if (gradeLetter != null)
            {
                gradeLetter.text = report.Letter.ToString();
                gradeLetter.color = report.Letter <= 'B' ? NoirPalette.InkRed : NoirPalette.Ink;
            }
            if (gradeScore != null) gradeScore.text = $"{report.Score}/100";
            if (gradeBreakdown != null) gradeBreakdown.text = Breakdown(report);

            backdrop?.SetShown(true);
            panel?.SetShown(true);
            actions?.Show(new[]
            {
                new MenuEntry("Újra", () => RetryRequested?.Invoke()),
                new MenuEntry("Főmenü", () => MainMenuRequested?.Invoke()),
            });
        }

        private static string OutcomeLine(InterrogationOutcome outcome, string suspect)
        {
            switch (outcome)
            {
                case InterrogationOutcome.Confessed: return $"{suspect} megtört és bevallotta a tettet.";
                case InterrogationOutcome.LawyeredUp: return $"{suspect} ügyvédet követelt — a kihallgatás megszakadt.";
                case InterrogationOutcome.OutOfQuestions: return "A szalag lejárt, a gyanúsított nem tört meg.";
                case InterrogationOutcome.Abandoned: return "A kihallgatást feladtuk — az ügy nyitva marad.";
                default: return string.Empty;
            }
        }

        private static string Findings(CaseData c, int rounds, IReadOnlyCollection<string> revealed, CaseGradeReport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<b>Felhasznált szalag:</b> {rounds}/{c.roundLimit} kérdés");
            sb.AppendLine($"<b>Feltárt ellentmondások:</b> {r.KeyRevealed}/{r.KeyTotal}");
            foreach (var e in c.contradictions)
            {
                if (e == null) continue;
                var known = Contains(revealed, e.factId);
                sb.AppendLine(known ? $"  — {Capitalize(e.evidenceLabel)}" : "  — <color=#8A7F6E>???</color>");
            }
            sb.AppendLine();
            sb.AppendLine("<b>Nyomok</b>");
            foreach (var f in c.facts)
            {
                if (f == null) continue;
                sb.AppendLine(Contains(revealed, f.id)
                    ? $"  — {CaseFilePanel.ClueText(f)}"
                    : $"  — <color=#8A7F6E>??? ({f.role}) — ki nem derített</color>");
            }
            return sb.ToString();
        }

        private static string Breakdown(CaseGradeReport r)
        {
            var sb = new StringBuilder();
            foreach (var item in r.Items)
            {
                var pts = Number(item.Points);
                var max = item.Max > 0f ? $" / {Number(item.Max)}" : string.Empty;
                sb.AppendLine($"{item.Label}  <pos=78%>{(item.Points > 0f ? "+" : string.Empty)}{pts}{max}");
            }
            sb.AppendLine();
            sb.Append($"<i>{CaseGrader.Verdict(r.Letter)}</i>");
            return sb.ToString();
        }

        // Magyar tizedesvessző, a gép területi beállításától függetlenül.
        private static string Number(float v) =>
            (v % 1f == 0f ? v.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
                          : v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Replace('.', ',');

        private static bool Contains(IReadOnlyCollection<string> set, string id)
        {
            if (set == null || string.IsNullOrEmpty(id)) return false;
            foreach (var s in set)
                if (string.Equals(s, id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? string.Empty : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
