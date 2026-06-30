namespace Interrogation.Fsm
{
    public enum SuspectState
    {
        Idle,
        Listening,
        Thinking,
        Responding,

        // Vég-állapotok
        Confessed,
        LawyeredUp,
        Silent      // elzárkózott vagy elfogyott a kör-limit
    }

    public static class SuspectStateExtensions
    {
        public static bool IsTerminal(this SuspectState state) =>
            state == SuspectState.Confessed ||
            state == SuspectState.LawyeredUp ||
            state == SuspectState.Silent;

        public static bool IsVictory(this SuspectState state) =>
            state == SuspectState.Confessed;
    }
}
