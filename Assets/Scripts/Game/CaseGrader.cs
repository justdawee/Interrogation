using System;
using System.Collections.Generic;
using Interrogation.Memory;

namespace Interrogation.Game
{
    public readonly struct GradeItem
    {
        public readonly string Label;
        public readonly float Points;
        public readonly float Max; // 0 = levonás (nincs felső korlát)

        public GradeItem(string label, float points, float max)
        {
            Label = label;
            Points = points;
            Max = max;
        }
    }

    public sealed class CaseGradeReport
    {
        public int Score { get; }
        public char Letter { get; }
        public IReadOnlyList<GradeItem> Items { get; }
        public int KeyRevealed { get; }
        public int KeyTotal { get; }
        public int OtherRevealed { get; }
        public int OtherTotal { get; }

        public CaseGradeReport(int score, char letter, IReadOnlyList<GradeItem> items,
            int keyRevealed, int keyTotal, int otherRevealed, int otherTotal)
        {
            Score = score;
            Letter = letter;
            Items = items;
            KeyRevealed = keyRevealed;
            KeyTotal = keyTotal;
            OtherRevealed = otherRevealed;
            OtherTotal = otherTotal;
        }
    }

    /// <summary>
    /// A nyomozói értékelés (A–D) a menet végén, a játékállapotból számolva (az LLM nem szól bele).
    /// Max. 100 pont: vallomás 30 · kulcs-ellentmondások 3×12 · további nyomok 4×2,5 ·
    /// hatékonyság 24 (csak vallomásnál) · alaptalan vád −4 / elvesztett türelem-pont.
    /// Jegy: A ≥ 85 · B ≥ 70 · C ≥ 45 · D; vallomás nélkül legfeljebb C.
    /// </summary>
    public static class CaseGrader
    {
        public const float ConfessionPoints = 30f;
        public const float KeyContradictionPoints = 36f;
        public const float OtherFactPoints = 10f;
        public const float EfficiencyPoints = 24f;
        public const float PatiencePenalty = 4f;
        public const int FullEfficiencyRounds = 8;

        public const int GradeA = 85;
        public const int GradeB = 70;
        public const int GradeC = 45;

        public static CaseGradeReport Grade(
            CaseData caseData,
            InterrogationOutcome outcome,
            int rounds,
            IReadOnlyCollection<string> revealedFactIds,
            int patienceLost)
        {
            if (caseData == null) throw new ArgumentNullException(nameof(caseData));

            var keyIds = KeyFactIds(caseData);
            var keyTotal = keyIds.Count;
            var otherTotal = 0;
            int keyRevealed = 0, otherRevealed = 0;
            foreach (var f in caseData.facts)
            {
                if (f == null || string.IsNullOrWhiteSpace(f.id)) continue;
                var known = Contains(revealedFactIds, f.id);
                if (keyIds.Contains(f.id)) { if (known) keyRevealed++; }
                else { otherTotal++; if (known) otherRevealed++; }
            }

            var confessed = outcome == InterrogationOutcome.Confessed;
            var items = new List<GradeItem>
            {
                new GradeItem("Vallomás", confessed ? ConfessionPoints : 0f, ConfessionPoints),
                new GradeItem($"Kulcs-ellentmondások ({keyRevealed}/{keyTotal})",
                    Share(KeyContradictionPoints, keyRevealed, keyTotal), KeyContradictionPoints),
                new GradeItem($"További nyomok ({otherRevealed}/{otherTotal})",
                    Share(OtherFactPoints, otherRevealed, otherTotal), OtherFactPoints),
                new GradeItem($"Hatékonyság ({rounds}/{caseData.roundLimit} kör)",
                    confessed ? Efficiency(rounds, caseData.roundLimit) : 0f, EfficiencyPoints),
            };
            if (patienceLost > 0)
                items.Add(new GradeItem($"Alaptalan vád (×{patienceLost})", -PatiencePenalty * patienceLost, 0f));

            var sum = 0f;
            foreach (var item in items) sum += item.Points;
            var score = Math.Max(0, Math.Min(100, (int)Math.Round(sum, MidpointRounding.AwayFromZero)));

            return new CaseGradeReport(score, Letter(score, confessed), items,
                keyRevealed, keyTotal, otherRevealed, otherTotal);
        }

        public static char Letter(int score, bool confessed)
        {
            var letter = score >= GradeA ? 'A' : score >= GradeB ? 'B' : score >= GradeC ? 'C' : 'D';
            return !confessed && letter < 'C' ? 'C' : letter;
        }

        /// <summary>≤ 8 kör = teljes pont, a kör-limitnél 0, köztük lineáris.</summary>
        public static float Efficiency(int rounds, int roundLimit)
        {
            var span = Math.Max(1, roundLimit - FullEfficiencyRounds);
            var k = (float)(roundLimit - rounds) / span;
            return EfficiencyPoints * Math.Max(0f, Math.Min(1f, k));
        }

        public static string Verdict(char letter)
        {
            switch (letter)
            {
                case 'A': return "Példás kihallgatás — gyors, bizonyítékokra épülő vallomás.";
                case 'B': return "Alapos munka — a vallomás megvan, de maradt kiaknázatlan nyom.";
                case 'C': return "Elfogadható — az ügy lezárható, de a munka hiányos.";
                default: return "Gyenge — az ügy nyitva marad, a gyanúsított kicsúszott a kezünkből.";
            }
        }

        private static HashSet<string> KeyFactIds(CaseData caseData)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (caseData.contradictions == null) return set;
            foreach (var c in caseData.contradictions)
                if (c != null && !string.IsNullOrWhiteSpace(c.factId)) set.Add(c.factId);
            return set;
        }

        private static float Share(float total, int got, int of) => of > 0 ? total * got / of : 0f;

        private static bool Contains(IReadOnlyCollection<string> set, string id)
        {
            if (set == null) return false;
            foreach (var s in set)
                if (string.Equals(s, id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
