using Interrogation.Game;

namespace Interrogation.Cameras
{
    public enum CameraShot
    {
        Menu,       // a megfigyelő-ablak felőli sarok
        Detective,  // over-the-shoulder, a kihallgatás alap-nézete
        Reaction,   // rövid közeli (rajtakapva)
        Breaking,   // lassú push-in az összeomláskor
        Ending      // tágabb kép a nem-vallomásos befejezésekhez
    }

    /// <summary>Melyik beállítás az alap az adott játékállapotban.</summary>
    public static class ShotRules
    {
        /// <summary>Csúcspont, „breaking" érzelem vagy vallomásos befejezés.</summary>
        public static bool IsBreaking(GamePhase phase, bool suspectBreaking, InterrogationOutcome? outcome)
        {
            switch (phase)
            {
                case GamePhase.Climax: return true;
                case GamePhase.Interrogation: return suspectBreaking;
                case GamePhase.Ending:
                case GamePhase.Summary: return outcome == InterrogationOutcome.Confessed;
                default: return false;
            }
        }

        // Felmutatás alatt mindig a detektív-nézet: a tárgy az ő kamerájához igazodik.
        public static CameraShot BaseShot(GamePhase phase, bool suspectBreaking, InterrogationOutcome? outcome, bool presenting)
        {
            switch (phase)
            {
                case GamePhase.Menu:
                case GamePhase.Briefing:
                    return CameraShot.Menu;
                case GamePhase.Intro:
                    return CameraShot.Detective;
                case GamePhase.Interrogation:
                case GamePhase.Climax:
                    if (presenting) return CameraShot.Detective;
                    return IsBreaking(phase, suspectBreaking, outcome) ? CameraShot.Breaking : CameraShot.Detective;
                default:
                    return IsBreaking(phase, suspectBreaking, outcome) ? CameraShot.Breaking : CameraShot.Ending;
            }
        }

        public static bool AllowsReaction(GamePhase phase, bool presenting) =>
            !presenting && (phase == GamePhase.Interrogation || phase == GamePhase.Climax);
    }
}
