using System.Text;

namespace Interrogation.Memory
{
    /// <summary>
    /// A vallomás-monológ promptja. Nyomás-győzelemnél a Game réteg már eldöntötte, hogy a
    /// gyanúsított megtört; a modell csak kimondja a vallomást a leleplezett tények alapján.
    /// </summary>
    public static class ConfessionPrompt
    {
        public const string UserMessage = "(A detektív csendben vár. A hazugságaid összeomlottak — most beszélsz.)";

        public static PromptContext Build(CaseData caseData, ConversationMemory memory, int lastStatements = 4)
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine(caseData.personaPrompt.Trim());
            sb.AppendLine();

            sb.AppendLine("A KIHALLGATÁS VÉGE: a bizonyítékok sarokba szorítottak, megtörtél. Most bevallod a tettet.");
            sb.AppendLine("Az igazság, amit bevallasz:");
            foreach (var fact in caseData.facts)
            {
                if (fact == null || !fact.isSecret) continue;
                sb.Append("- ").AppendLine(ContextBuilder.Compact(fact.content));
            }

            if (memory != null && memory.RevealedFactIds.Count > 0)
                sb.Append("A detektív már leleplezte: ").AppendLine(string.Join(", ", memory.RevealedFactIds));

            if (memory != null)
            {
                var recent = memory.LastStatements(lastStatements);
                if (recent.Count > 0)
                {
                    sb.AppendLine("Az utolsó állításaid (ezekre visszautalhatsz):");
                    foreach (var st in recent)
                        sb.Append('[').Append(st.Id).Append("] „").Append(ContextBuilder.Compact(st.Dialogue)).AppendLine("\"");
                }
            }

            sb.AppendLine();
            sb.AppendLine("A válaszod JSON: dialogue = a vallomásod 2-4 mondatban, magyarul, karakterben (mit tettél és miért, " +
                          "megtört hangon); emotion=breaking; confession=true; revealed_fact_id=null; " +
                          "contradiction.detected=false; stress_delta=0.");

            return new PromptContext(sb.ToString(), UserMessage);
        }
    }
}
