using System;
using System.Collections.Generic;
using System.Linq;

namespace Interrogation.Memory
{
    /// <summary>
    /// Csak olvasható nézet az eset tudásgráfjára: id szerinti keresés és a kérdéshez
    /// releváns részgráf kiválasztása.
    /// </summary>
    public sealed class KnowledgeGraph
    {
        private readonly IReadOnlyList<FactNode> _facts;
        private readonly Dictionary<string, FactNode> _byId;

        public KnowledgeGraph(IEnumerable<FactNode> facts)
        {
            _facts = (facts ?? Enumerable.Empty<FactNode>())
                .Where(f => f != null && !string.IsNullOrWhiteSpace(f.id))
                .ToList();

            _byId = new Dictionary<string, FactNode>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in _facts)
                _byId[f.id] = f;
        }

        public IReadOnlyList<FactNode> All => _facts;

        public FactNode GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return _byId.TryGetValue(id, out var node) ? node : null;
        }

        public bool TryGetById(string id, out FactNode node)
        {
            node = GetById(id);
            return node != null;
        }

        /// <summary>
        /// A legjobban illeszkedő tények csökkenő relevancia szerint. Találat nélkül üres listát ad.
        /// </summary>
        public IReadOnlyList<FactNode> SelectRelevant(string query, int maxResults = 4)
        {
            if (maxResults <= 0 || _facts.Count == 0)
                return Array.Empty<FactNode>();

            var queryTokens = Tokenize(query);
            if (queryTokens.Count == 0)
                return Array.Empty<FactNode>();

            return _facts
                .Select(f => new { Fact = f, Score = ScoreFact(f, queryTokens) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(maxResults)
                .Select(x => x.Fact)
                .ToList();
        }

        private static int ScoreFact(FactNode fact, HashSet<string> queryTokens)
        {
            int score = 0;

            if (fact.keywords != null)
            {
                foreach (var kw in fact.keywords)
                {
                    foreach (var token in Tokenize(kw))
                    {
                        if (queryTokens.Contains(token)) score += 3;
                    }
                }
            }

            foreach (var token in Tokenize(fact.content))
            {
                if (queryTokens.Contains(token)) score += 1;
            }

            foreach (var token in Tokenize(fact.role))
            {
                if (queryTokens.Contains(token)) score += 1;
            }

            return score;
        }

        private static HashSet<string> Tokenize(string text)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text)) return set;

            var current = new System.Text.StringBuilder();
            foreach (var ch in text)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    current.Append(char.ToLowerInvariant(ch));
                }
                else
                {
                    AddToken(set, current);
                }
            }
            AddToken(set, current);
            return set;
        }

        private static void AddToken(HashSet<string> set, System.Text.StringBuilder current)
        {
            if (current.Length >= 3) // a rövid kötőszavak csak zajt adnának
                set.Add(current.ToString());
            current.Clear();
        }
    }
}
