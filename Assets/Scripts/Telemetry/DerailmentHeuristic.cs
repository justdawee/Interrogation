using System.Collections.Generic;
using System.Linq;

namespace Interrogation.Telemetry
{
    /// <summary>
    /// Heurisztikus derailment-jelölő a mérésekhez; csak a naplóba jelöl, a játékmenetbe nem avatkozik.
    /// Jelölők: <c>meta</c> (modell/szabály emlegetése), <c>english</c>, <c>long</c> (több mint 3 mondat), <c>empty</c>.
    /// </summary>
    public static class DerailmentHeuristic
    {
        public const int MaxSentences = 3;

        private static readonly string[] MetaPhrases =
        {
            "mesterséges intelligencia", "nyelvi modell", "nyelvmodell", "chatgpt", "openai",
            "asszisztens vagyok", "as an ai", "language model", "i am an ai", "i'm an ai", "prompt"
        };

        private static readonly HashSet<string> EnglishWords = new HashSet<string>
        {
            "the", "and", "you", "i'm", "not", "was", "with", "what", "that", "this", "have", "don't"
        };

        private const string HungarianLetters = "áéíóöőúüűÁÉÍÓÖŐÚÜŰ";

        /// <summary>A talált jelölők „|"-lel elválasztva; üres, ha nincs.</summary>
        public static string Detect(string dialogue)
        {
            if (string.IsNullOrWhiteSpace(dialogue)) return "empty";

            var flags = new List<string>(3);
            var lower = dialogue.ToLowerInvariant();

            if (MetaPhrases.Any(lower.Contains)) flags.Add("meta");
            if (LooksEnglish(lower)) flags.Add("english");
            if (CountSentences(dialogue) > MaxSentences) flags.Add("long");

            return string.Join("|", flags);
        }

        private static bool LooksEnglish(string lower)
        {
            if (lower.IndexOfAny(HungarianLetters.ToCharArray()) >= 0) return false;
            var words = lower.Split(new[] { ' ', ',', '.', '!', '?', ';', ':', '"', '„' }, System.StringSplitOptions.RemoveEmptyEntries);
            return words.Count(EnglishWords.Contains) >= 2;
        }

        // Az írásjel csak akkor zár mondatot, ha szövegvég vagy nagybetű követi, így a
        // közbeékelt kipontozás („De… igen") nem számít külön mondatnak.
        private static int CountSentences(string text)
        {
            var trimmed = text.Trim();
            int count = 0;
            int i = 0;
            while (i < trimmed.Length)
            {
                if (!IsTerminator(trimmed[i])) { i++; continue; }
                while (i < trimmed.Length && IsTerminator(trimmed[i])) i++;
                var next = i;
                while (next < trimmed.Length && (char.IsWhiteSpace(trimmed[next]) || trimmed[next] == '"' || trimmed[next] == '”')) next++;
                if (next >= trimmed.Length || char.IsUpper(trimmed[next])) count++;
            }
            var tail = trimmed.TrimEnd('"', '”');
            if (tail.Length > 0 && !IsTerminator(tail[tail.Length - 1])) count++;
            return count;
        }

        private static bool IsTerminator(char c) => c == '.' || c == '!' || c == '?' || c == '…';
    }
}
