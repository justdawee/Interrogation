using Interrogation.Llm;

namespace Interrogation.Game
{
    public readonly struct TurnResult
    {
        /// <summary>Az őr által szűrt válasz.</summary>
        public readonly SuspectReply Reply;
        /// <summary>A nyers LLM-eredmény, benne a szűretlen válasszal.</summary>
        public readonly LlmResult LlmResult;
        public readonly GuardVerdict Guard;
        public readonly InterrogationOutcome Outcome;
        public readonly int Round;
        public readonly int Stress;
        /// <summary>A mérőre alkalmazott, szorzott változás.</summary>
        public readonly int StressDelta;
        /// <summary>Szorzatlan pontok (stress_delta + ellentmondás-bónusz).</summary>
        public readonly int StressPoints;
        public readonly string Question;
        public readonly string EvidenceId;

        public TurnResult(SuspectReply reply, LlmResult llmResult, GuardVerdict guard, InterrogationOutcome outcome,
            int round, int stress, int stressDelta, int stressPoints, string question, string evidenceId)
        {
            Reply = reply;
            LlmResult = llmResult;
            Guard = guard;
            Outcome = outcome;
            Round = round;
            Stress = stress;
            StressDelta = stressDelta;
            StressPoints = stressPoints;
            Question = question;
            EvidenceId = evidenceId;
        }

        /// <summary>
        /// Nyomás-győzelem, de a válasz nem vallomás: ez a mondat nem hangzik el, egyből a
        /// vallomás-monológ következik.
        /// </summary>
        public bool IsSupersededByConfession =>
            Outcome == InterrogationOutcome.Confessed && Reply != null && !Reply.Confession;

        public SuspectReply RawReply => LlmResult != null ? LlmResult.Reply : Reply;
    }
}
