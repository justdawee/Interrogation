using Interrogation.Llm;

namespace Interrogation.Memory
{
    /// <summary>
    /// A gyanúsított egy elhangzott állítása. Az <see cref="Id"/>-re a későbbi körök hivatkozhatnak.
    /// </summary>
    public sealed class SuspectStatement
    {
        public string Id { get; }
        public int Round { get; }            // 1-alapú
        public string PlayerQuestion { get; }
        public string Dialogue { get; }
        public SuspectEmotion Emotion { get; }
        public int StressAfter { get; }

        public SuspectStatement(
            string id,
            int round,
            string playerQuestion,
            string dialogue,
            SuspectEmotion emotion,
            int stressAfter)
        {
            Id = id;
            Round = round;
            PlayerQuestion = playerQuestion;
            Dialogue = dialogue;
            Emotion = emotion;
            StressAfter = stressAfter;
        }
    }
}
