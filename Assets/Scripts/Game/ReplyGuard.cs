using System;
using System.Collections.Generic;
using Interrogation.Llm;
using Interrogation.Memory;

namespace Interrogation.Game
{
    public readonly struct GuardVerdict
    {
        public readonly SuspectReply Reply;
        public readonly bool ContradictionRejected;
        public readonly bool FactRejected;
        public readonly string ExposedFactId;
        public readonly string Note;         // indoklás a telemetriához; üres, ha nem volt beavatkozás

        public GuardVerdict(SuspectReply reply, bool contradictionRejected, bool factRejected, string exposedFactId, string note)
        {
            Reply = reply;
            ContradictionRejected = contradictionRejected;
            FactRejected = factRejected;
            ExposedFactId = exposedFactId;
            Note = note ?? string.Empty;
        }

        public bool Intervened => ContradictionRejected || FactRejected;
    }

    /// <summary>
    /// Az LLM <c>contradiction</c> és <c>revealed_fact_id</c> állításait ellenőrzi, mielőtt a mechanikára
    /// hatnának (hallucináció elleni korlát):
    /// • Egy még le nem leplezett kulcs-tény bizonyítéka mindig leleplez és ellentmondásnak számít,
    ///   akkor is, ha a modell kitér. Ismételt kijátszás nem ad újabb bónuszt.
    /// • Bizonyíték nélküli ellentmondás csak létező korábbi állításra (s#) és témába vágó kérdésre.
    /// • Kulcs-tény csak a saját bizonyítékával vagy témába vágó, elfogadott ellentmondással leplezhető le.
    /// </summary>
    public sealed class ReplyGuard
    {
        private const int TopicFacts = 3;

        private readonly KnowledgeGraph _graph;
        private readonly HashSet<string> _keyFactIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ReplyGuard(CaseData caseData)
        {
            if (caseData == null) throw new ArgumentNullException(nameof(caseData));
            _graph = caseData.BuildKnowledgeGraph();
            if (caseData.contradictions == null) return;
            foreach (var c in caseData.contradictions)
                if (c != null && !string.IsNullOrWhiteSpace(c.factId)) _keyFactIds.Add(c.factId);
        }

        /// <param name="memory">A kör előtti állapot, így csak valóban elhangzott s#-re lehet hivatkozni.</param>
        public GuardVerdict Review(SuspectReply reply, string question, ContradictionPair evidence, ConversationMemory memory)
        {
            if (reply == null) throw new ArgumentNullException(nameof(reply));

            var topic = TopicFactIds(question, evidence);
            var notes = new List<string>(2);

            var keyEvidence = evidence != null && _keyFactIds.Contains(evidence.factId);
            var refutesNew = keyEvidence && (memory == null || !memory.HasRevealed(evidence.factId));

            var contradiction = ReviewContradiction(reply.Contradiction, evidence, keyEvidence && !refutesNew, topic, memory,
                notes, out var contraRejected);

            // A cáfolat a Body ténye: a bizonyíték akkor is ellentmondás, ha az LLM kitérést jelzett.
            var imposed = refutesNew && !contradiction.Detected;
            if (imposed)
            {
                contradiction = new Contradiction { Detected = true, WithStatementId = null };
                notes.Add($"szembesítés: a bizonyíték cáfolja ({evidence.factId}), az LLM kitért — a Body rögzíti");
            }

            var factId = ReviewFact(reply.RevealedFactId, evidence, topic, contradiction.Detected, notes, out var factRejected);
            var exposed = refutesNew ? evidence.factId : null;

            var filtered = contraRejected || factRejected || imposed
                ? new SuspectReply(reply.Dialogue, reply.Emotion, factId, contradiction, reply.Confession, reply.StressDelta)
                : reply;
            return new GuardVerdict(filtered, contraRejected, factRejected, exposed, string.Join("; ", notes));
        }

        private Contradiction ReviewContradiction(Contradiction c, ContradictionPair evidence, bool alreadyExposed,
            HashSet<string> topic, ConversationMemory memory, List<string> notes, out bool rejected)
        {
            rejected = false;
            if (!c.Detected) return c;

            if (evidence != null)
            {
                if (!alreadyExposed) return c;
                rejected = true;
                notes.Add($"ellentmondás elvetve: a bizonyíték tényét már leleplezték ({evidence.factId})");
                return Contradiction.None;
            }

            if (!StatementExists(memory, c.WithStatementId))
            {
                rejected = true;
                notes.Add($"ellentmondás elvetve: nem létező állítás ({c.WithStatementId ?? "null"})");
                return Contradiction.None;
            }
            if (topic.Count == 0)
            {
                rejected = true;
                notes.Add("ellentmondás elvetve: a kérdés egyik tényhez sem kötődik");
                return Contradiction.None;
            }
            return c;
        }

        private string ReviewFact(string factId, ContradictionPair evidence, HashSet<string> topic,
            bool contradictionAccepted, List<string> notes, out bool rejected)
        {
            rejected = false;
            if (string.IsNullOrWhiteSpace(factId)) return null;

            var fact = _graph.GetById(factId);
            if (fact == null)
            {
                rejected = true;
                notes.Add($"tény elvetve: ismeretlen id ({factId})");
                return null;
            }
            if (!_keyFactIds.Contains(fact.id)) return fact.id;

            var ownEvidence = evidence != null && string.Equals(evidence.factId, fact.id, StringComparison.OrdinalIgnoreCase);
            if (ownEvidence || (contradictionAccepted && topic.Contains(fact.id))) return fact.id;

            // A hazugság puszta megismétlése (pl. „otthon voltam") nem leleplezés.
            rejected = true;
            notes.Add($"kulcs-tény elvetve: nincs bizonyíték és elfogadott ellentmondás ({fact.id})");
            return null;
        }

        private HashSet<string> TopicFactIds(string question, ContradictionPair evidence)
        {
            var query = evidence == null ? question : $"{question} {evidence.evidenceLabel} {evidence.description}";
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in _graph.SelectRelevant(query, TopicFacts)) set.Add(f.id);
            return set;
        }

        private static bool StatementExists(ConversationMemory memory, string statementId)
        {
            if (memory == null || string.IsNullOrWhiteSpace(statementId)) return false;
            foreach (var s in memory.Statements)
                if (string.Equals(s.Id, statementId.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
