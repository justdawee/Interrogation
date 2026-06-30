using System;
using System.Collections.Generic;

namespace Interrogation.Fsm
{
    /// <summary>
    /// A gyanúsított determinisztikus állapotgépe. Csak az engedélyezett átmeneteket őrzi;
    /// érvénytelen átmenetre kivételt dob, így hibás vezérlés nem csúszhat át csendben.
    /// </summary>
    public sealed class SuspectStateMachine
    {
        private static readonly IReadOnlyDictionary<SuspectState, HashSet<SuspectState>> Allowed =
            new Dictionary<SuspectState, HashSet<SuspectState>>
            {
                [SuspectState.Idle] = new HashSet<SuspectState> { SuspectState.Listening },
                [SuspectState.Listening] = new HashSet<SuspectState>
                {
                    SuspectState.Thinking,
                    // korai vég-állapotok (pl. kör-limit)
                    SuspectState.LawyeredUp, SuspectState.Silent
                },
                [SuspectState.Thinking] = new HashSet<SuspectState> { SuspectState.Responding },
                [SuspectState.Responding] = new HashSet<SuspectState>
                {
                    SuspectState.Listening,
                    SuspectState.Confessed, SuspectState.LawyeredUp, SuspectState.Silent
                },
                [SuspectState.Confessed] = new HashSet<SuspectState>(),
                [SuspectState.LawyeredUp] = new HashSet<SuspectState>(),
                [SuspectState.Silent] = new HashSet<SuspectState>()
            };

        public SuspectState Current { get; private set; } = SuspectState.Idle;

        public bool IsTerminal => Current.IsTerminal();

        public event Action<SuspectState, SuspectState> StateChanged;

        public bool CanTransition(SuspectState to) =>
            Allowed.TryGetValue(Current, out var set) && set.Contains(to);

        public void Transition(SuspectState to)
        {
            if (!CanTransition(to))
                throw new InvalidOperationException(
                    $"Érvénytelen FSM-átmenet: {Current} -> {to}.");

            var from = Current;
            Current = to;
            StateChanged?.Invoke(from, to);
        }

        public void Reset()
        {
            var from = Current;
            Current = SuspectState.Idle;
            if (from != SuspectState.Idle)
                StateChanged?.Invoke(from, SuspectState.Idle);
        }
    }
}
