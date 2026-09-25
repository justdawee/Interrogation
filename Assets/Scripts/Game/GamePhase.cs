using System;

namespace Interrogation.Game
{
    public enum GamePhase
    {
        Menu,
        Briefing,
        Intro,
        Interrogation,
        Climax,
        Ending,
        Summary
    }

    /// <summary>
    /// A játékfolyam őrzött fázis-állapotgépe (a gyanúsított FSM-je egy kört, ez a teljes menetet
    /// vezérli). Érvénytelen átmenetre kivételt dob.
    /// </summary>
    public sealed class GamePhaseMachine
    {
        public GamePhase Current { get; private set; }

        public event Action<GamePhase, GamePhase> Changed;

        public GamePhaseMachine(GamePhase start = GamePhase.Menu) => Current = start;

        public static bool IsAllowed(GamePhase from, GamePhase to)
        {
            switch (from)
            {
                case GamePhase.Menu: return to == GamePhase.Briefing || to == GamePhase.Intro;
                case GamePhase.Briefing: return to == GamePhase.Intro || to == GamePhase.Menu;
                case GamePhase.Intro: return to == GamePhase.Interrogation;
                case GamePhase.Interrogation: return to == GamePhase.Climax || to == GamePhase.Ending;
                case GamePhase.Climax: return to == GamePhase.Ending;
                case GamePhase.Ending: return to == GamePhase.Summary;
                case GamePhase.Summary: return to == GamePhase.Menu || to == GamePhase.Intro;
                default: return false;
            }
        }

        public void Transition(GamePhase to)
        {
            if (!IsAllowed(Current, to))
                throw new InvalidOperationException($"Érvénytelen fázis-átmenet: {Current} → {to}.");
            var from = Current;
            Current = to;
            Changed?.Invoke(from, to);
        }

        public bool IsPlayable => Current == GamePhase.Interrogation || Current == GamePhase.Climax;
    }
}
