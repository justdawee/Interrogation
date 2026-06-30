using System.Collections.Generic;
using System.Text;

namespace Interrogation.Memory
{
    public readonly struct PromptContext
    {
        public readonly string SystemPrompt;
        public readonly string UserMessage;

        public PromptContext(string systemPrompt, string userMessage)
        {
            SystemPrompt = systemPrompt;
            UserMessage = userMessage;
        }
    }

    /// <summary>
    /// A system prompt determinisztikus, tömör összeállítása: perszóna, a kérdéshez releváns
    /// tudás-részgráf, az utolsó N állítás és a futó állapot. A túlzsúfolt prompt növeli a
    /// szerepből kilépés (derailment) esélyét, ezért csak a releváns rész kerül be.
    /// </summary>
    public static class ContextBuilder
    {
        public const int DefaultMaxRelevantFacts = 4;
        public const int DefaultLastStatements = 6;

        public static PromptContext Build(
            CaseData caseData,
            KnowledgeGraph graph,
            ConversationMemory memory,
            string playerQuestion,
            int currentStress,
            ContradictionPair evidence = null,
            int maxRelevantFacts = DefaultMaxRelevantFacts,
            int lastStatements = DefaultLastStatements)
        {
            var sb = new StringBuilder(1024);

            sb.AppendLine(caseData.personaPrompt.Trim());
            sb.AppendLine();

            // A sémát a Structured Outputs kényszeríti ki; itt a mezők jelentését tisztázzuk.
            AppendContractGuide(sb);
            sb.AppendLine();

            // Találat nélkül a teljes titok-halmaz helyett csak rövid figyelmeztetés kerül be.
            AppendKnowledge(sb, graph, playerQuestion, evidence, maxRelevantFacts);
            sb.AppendLine();

            if (evidence != null)
            {
                AppendEvidence(sb, evidence);
                sb.AppendLine();
            }

            // Az id-k kellenek az önellentmondás jelöléséhez (contradiction.with_statement_id).
            AppendMemory(sb, memory, lastStatements);
            sb.AppendLine();

            sb.Append("Aktuális állapot: stressz=").Append(currentStress)
              .Append('/').Append(caseData.stressThreshold)
              .Append(", eddigi kérdések=").Append(memory.Round)
              .Append('/').Append(caseData.roundLimit).Append('.');

            return new PromptContext(sb.ToString(), ComposeUserMessage(playerQuestion, evidence));
        }

        /// <summary>A bizonyíték részletei a system promptban vannak, itt csak rövid jelölés áll.</summary>
        public static string ComposeUserMessage(string question, ContradictionPair evidence)
        {
            question = question?.Trim() ?? string.Empty;
            if (evidence == null)
                return question.Length > 0 ? question : "(a detektív hallgat)";

            var marker = $"[BIZONYÍTÉK: {evidence.evidenceLabel}]";
            return question.Length > 0
                ? $"{marker} {question}"
                : $"{marker} (A detektív szó nélkül eléd teszi a bizonyítékot.)";
        }

        private static void AppendContractGuide(StringBuilder sb)
        {
            sb.AppendLine("A válaszod egy JSON-objektum a következő mezőkkel:");
            sb.AppendLine("- dialogue: a gyanúsított szövege (1-3 mondat, magyarul, karakterben).");
            sb.AppendLine("- emotion: calm | nervous | defensive | hostile | breaking — az aktuális hangulatod.");
            sb.AppendLine("- revealed_fact_id: ha a válaszoddal kicsúszik egy konkrét rejtett tény, annak az id-ja, különben null.");
            sb.AppendLine("- contradiction.detected + with_statement_id: ha a mostani szavaid ütköznek egy KORÁBBI állításoddal (s#), jelöld true-ra és add meg az id-t.");
            sb.AppendLine("- confession: csak akkor true, ha tényleg beismered a tettet.");
            sb.AppendLine("- stress_delta: a kérdés fenyegetése szerint -3..+3 (semleges 0/+1, motívum +2, közvetlen szembesítés +3, empátia -1).");
        }

        private static void AppendKnowledge(StringBuilder sb, KnowledgeGraph graph, string query,
            ContradictionPair evidence, int max)
        {
            var relevant = SelectWithEvidence(graph, query, evidence, max);

            sb.AppendLine("A rejtett tudásod (a játékos NEM látja; titkokat védd, ne valld be könnyen):");
            if (relevant.Count == 0)
            {
                sb.AppendLine("- (a mostani kérdés egyik konkrét tényhez sem kötődik közvetlenül — maradj óvatos és kitérő.)");
                return;
            }

            foreach (var fact in relevant)
            {
                var tag = fact.isSecret ? "TITOK" : "nyomás";
                sb.Append("- [").Append(fact.id).Append("] (").Append(tag).Append(") ")
                  .AppendLine(Compact(fact.content));
            }
        }

        // A cáfolt tény a kulcsszó-illesztéstől függetlenül mindig az első helyre kerül.
        private static List<FactNode> SelectWithEvidence(KnowledgeGraph graph, string query,
            ContradictionPair evidence, int max)
        {
            var result = new List<FactNode>(max);
            var target = evidence != null ? graph.GetById(evidence.factId) : null;
            if (target != null) result.Add(target);

            var fullQuery = evidence == null ? query : $"{query} {evidence.evidenceLabel} {evidence.description}";
            foreach (var fact in graph.SelectRelevant(fullQuery, max))
            {
                if (result.Count >= max) break;
                if (!result.Contains(fact)) result.Add(fact);
            }
            return result;
        }

        private static void AppendEvidence(StringBuilder sb, ContradictionPair evidence)
        {
            sb.Append("SZEMBESÍTÉS: a detektív most eléd teszi ezt a bizonyítékot: ")
              .Append(Compact(evidence.evidenceLabel)).Append(" — ").AppendLine(Compact(evidence.description));
            sb.Append("Ez a [").Append(evidence.factId).Append("] tényhez tartozó hazugságodat VITATHATATLANUL cáfolja — ")
              .Append("a detektív tudja, hogy hazudtál, a hazugság így nem tartható fenn. Csak a reakciód a tiéd ")
              .Append("(döbbenet, magyarázkodás, düh, részleges beismerés), karakterben. contradiction.detected=true; ")
              .Append("ha beismered a tényt, revealed_fact_id=").Append(evidence.factId).AppendLine(".");
        }

        private static void AppendMemory(StringBuilder sb, ConversationMemory memory, int lastN)
        {
            var recent = memory.LastStatements(lastN);
            if (recent.Count == 0)
            {
                sb.AppendLine("Eddig még nem szólaltál meg ebben a kihallgatásban.");
                return;
            }

            sb.AppendLine("A KORÁBBI állításaid (maradj velük konzisztens, vagy vállald az ellentmondást):");
            foreach (var st in recent)
            {
                sb.Append('[').Append(st.Id).Append("] „").Append(Compact(st.Dialogue)).Append('"');
                sb.AppendLine();
            }
        }

        internal static string Compact(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var sb = new StringBuilder(text.Length);
            bool prevSpace = false;
            foreach (var ch in text)
            {
                var c = (ch == '\n' || ch == '\r' || ch == '\t') ? ' ' : ch;
                if (c == ' ')
                {
                    if (prevSpace) continue;
                    prevSpace = true;
                }
                else prevSpace = false;
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }
    }
}
