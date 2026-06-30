using System;
using System.Collections.Generic;
using System.Linq;
using Interrogation.Llm;

namespace Interrogation.Memory
{
    /// <summary>
    /// A kihallgatás memóriája: a gyanúsított állításai, a felfedett tények és a stressz-történet.
    /// Csak bővül, meglévő állítás nem íródik felül. Az id-ket ("s1", "s2", …) ez az osztály osztja ki.
    /// </summary>
    public sealed class ConversationMemory
    {
        private readonly List<SuspectStatement> _statements = new List<SuspectStatement>();
        private readonly HashSet<string> _revealedFactIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<int> _stressHistory = new List<int>();

        public IReadOnlyList<SuspectStatement> Statements => _statements;
        public IReadOnlyCollection<string> RevealedFactIds => _revealedFactIds;
        public IReadOnlyList<int> StressHistory => _stressHistory;

        public int Round => _statements.Count;

        public int CurrentStress => _stressHistory.Count > 0 ? _stressHistory[_stressHistory.Count - 1] : 0;

        /// <param name="exposedFactId">A bizonyítékkal leleplezett tény, ha van.</param>
        public SuspectStatement Append(
            string playerQuestion,
            SuspectReply reply,
            int stressAfter,
            string exposedFactId = null)
        {
            if (reply == null) throw new ArgumentNullException(nameof(reply));

            var round = _statements.Count + 1;
            var id = "s" + round;

            var statement = new SuspectStatement(
                id,
                round,
                playerQuestion,
                reply.Dialogue,
                reply.Emotion,
                stressAfter);

            _statements.Add(statement);
            _stressHistory.Add(stressAfter);

            if (!string.IsNullOrWhiteSpace(reply.RevealedFactId))
                _revealedFactIds.Add(reply.RevealedFactId);
            if (!string.IsNullOrWhiteSpace(exposedFactId))
                _revealedFactIds.Add(exposedFactId);

            return statement;
        }

        public bool HasRevealed(string factId) =>
            !string.IsNullOrWhiteSpace(factId) && _revealedFactIds.Contains(factId);

        public IReadOnlyList<SuspectStatement> LastStatements(int n)
        {
            if (n <= 0 || _statements.Count == 0)
                return Array.Empty<SuspectStatement>();

            var skip = Math.Max(0, _statements.Count - n);
            return _statements.Skip(skip).ToList();
        }
    }
}
