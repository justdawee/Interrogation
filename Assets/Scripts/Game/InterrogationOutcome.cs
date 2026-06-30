using Interrogation.Fsm;

namespace Interrogation.Game
{
    public enum InterrogationOutcome
    {
        Continue,
        Confessed,
        LawyeredUp,
        OutOfQuestions,     // elfogyott a kör-limit
        Abandoned           // a detektív feladta
    }

    public static class InterrogationOutcomeExtensions
    {
        public static SuspectState ToState(this InterrogationOutcome outcome)
        {
            switch (outcome)
            {
                case InterrogationOutcome.Confessed: return SuspectState.Confessed;
                case InterrogationOutcome.LawyeredUp: return SuspectState.LawyeredUp;
                case InterrogationOutcome.OutOfQuestions: return SuspectState.Silent;
                case InterrogationOutcome.Abandoned: return SuspectState.Silent;
                default: return SuspectState.Listening;
            }
        }

        public static bool IsTerminal(this InterrogationOutcome outcome) =>
            outcome != InterrogationOutcome.Continue;

        public static bool IsVictory(this InterrogationOutcome outcome) =>
            outcome == InterrogationOutcome.Confessed;
    }
}
