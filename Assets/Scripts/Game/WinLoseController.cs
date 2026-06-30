using System;
using System.Collections.Generic;
using Interrogation.Llm;
using Interrogation.Memory;

namespace Interrogation.Game
{
    /// <summary>
    /// A menet vég-feltételei körről körre; az LLM sosem dönt közvetlenül győzelemről.
    /// • Győzelem: <c>confession=true</c>, vagy stressz a küszöbön és elég kulcs-ellentmondás (a kötelezővel).
    /// • Vereség: az alá nem támasztott vádak kimerítik a türelmet, vagy elfogy a kör-limit.
    /// </summary>
    public sealed class WinLoseController
    {
        private const int AggressiveDelta = 3;  // közvetlen vád
        private const int EmpathyDelta = -1;     // „jó zsaru", visszaépíti a türelmet
        public const int PatienceMax = 3;

        private readonly HashSet<string> _contradictionFactIds;
        private readonly string _requiredFactId;
        private readonly int _minKeyContradictions;

        private int _patience;

        public WinLoseController(CaseData caseData)
        {
            if (caseData == null) throw new ArgumentNullException(nameof(caseData));

            _contradictionFactIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (caseData.contradictions != null)
            {
                foreach (var c in caseData.contradictions)
                {
                    if (c != null && !string.IsNullOrWhiteSpace(c.factId))
                        _contradictionFactIds.Add(c.factId);
                }
            }

            _requiredFactId = string.IsNullOrWhiteSpace(caseData.requiredContradictionFactId)
                ? null
                : caseData.requiredContradictionFactId;
            _minKeyContradictions = Math.Max(1, caseData.minKeyContradictionsToBreak);
            _patience = PatienceMax;
        }

        public int Patience => _patience;

        public int CountKeyRevealed(IReadOnlyCollection<string> revealedFactIds)
        {
            if (revealedFactIds == null) return 0;
            int count = 0;
            foreach (var id in revealedFactIds)
                if (_contradictionFactIds.Contains(id)) count++;
            return count;
        }

        /// <summary>A hívó már alkalmazta a stresszt és bővítette a memóriát.</summary>
        /// <param name="evidencePresented">Bizonyítékos szembesítés sosem fogyaszt türelmet.</param>
        public InterrogationOutcome Evaluate(
            StressMeter stress,
            IReadOnlyCollection<string> revealedFactIds,
            int roundCount,
            int roundLimit,
            SuspectReply reply,
            int turnStressDelta,
            bool evidencePresented = false)
        {
            if (stress == null) throw new ArgumentNullException(nameof(stress));
            if (reply == null) throw new ArgumentNullException(nameof(reply));

            var keyRevealed = CountKeyRevealed(revealedFactIds);

            // Az alá nem támasztott vád a menet egészében fogyasztja a türelmet, az empátia visszaépíti.
            var supported = evidencePresented || reply.Contradiction.Detected;
            if (turnStressDelta >= AggressiveDelta && !supported)
                _patience--;
            else if (turnStressDelta <= EmpathyDelta)
                _patience = Math.Min(_patience + 1, PatienceMax);

            var pressureMet = stress.AtThreshold
                              && keyRevealed >= _minKeyContradictions
                              && RequiredRevealed(revealedFactIds);
            if (reply.Confession || pressureMet)
                return InterrogationOutcome.Confessed;

            if (_patience <= 0)
                return InterrogationOutcome.LawyeredUp;

            // A győzelem után ellenőrizzük, hogy az utolsó körös vallomás még győzzön.
            if (roundCount >= roundLimit)
                return InterrogationOutcome.OutOfQuestions;

            return InterrogationOutcome.Continue;
        }

        public void Reset() => _patience = PatienceMax;

        private bool RequiredRevealed(IReadOnlyCollection<string> revealedFactIds)
        {
            if (_requiredFactId == null) return true;
            if (revealedFactIds == null) return false;
            foreach (var id in revealedFactIds)
                if (string.Equals(id, _requiredFactId, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
