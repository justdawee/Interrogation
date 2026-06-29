using System;

namespace Interrogation.Llm
{
    public enum SuspectEmotion
    {
        Calm,
        Nervous,
        Defensive,
        Hostile,
        Breaking
    }

    /// <summary>A válasz ütközik-e a gyanúsított egy korábbi állításával.</summary>
    [Serializable]
    public struct Contradiction
    {
        public bool Detected;
        public string WithStatementId; // null = nincs konkrét hivatkozás

        public static Contradiction None => new Contradiction { Detected = false, WithStatementId = null };
    }

    /// <summary>A JSON-kontraktus validált, immutábilis alakja.</summary>
    public sealed class SuspectReply
    {
        public string Dialogue { get; }
        public SuspectEmotion Emotion { get; }
        public string RevealedFactId { get; } // null = nem fedett fel új tényt
        public Contradiction Contradiction { get; }
        public bool Confession { get; }
        public int StressDelta { get; }

        public SuspectReply(
            string dialogue,
            SuspectEmotion emotion,
            string revealedFactId,
            Contradiction contradiction,
            bool confession,
            int stressDelta)
        {
            Dialogue = dialogue;
            Emotion = emotion;
            RevealedFactId = revealedFactId;
            Contradiction = contradiction;
            Confession = confession;
            StressDelta = stressDelta;
        }

        /// <summary>Hiba esetén a gyanúsított hallgat; játékállapot nem változik.</summary>
        public static SuspectReply Fallback(string reason = null)
        {
            var line = string.IsNullOrEmpty(reason)
                ? "A gyanúsított hallgat, és összeszorítja az ajkát."
                : "A gyanúsított hallgat.";
            return new SuspectReply(
                line,
                SuspectEmotion.Defensive,
                revealedFactId: null,
                contradiction: Contradiction.None,
                confession: false,
                stressDelta: 0);
        }

        public override string ToString()
        {
            var c = Contradiction.Detected ? $"!{Contradiction.WithStatementId ?? "?"}" : "-";
            return $"[{Emotion}] stress{StressDelta:+0;-0;0} fact={RevealedFactId ?? "-"} contra={c} " +
                   $"confess={Confession} :: \"{Dialogue}\"";
        }
    }
}
