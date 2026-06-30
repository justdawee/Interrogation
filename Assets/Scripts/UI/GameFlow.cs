using Interrogation.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Interrogation.UI
{
    public static class SceneNames
    {
        public const string MainMenu = "MainMenu";
        public const string Interrogation = "Interrogation";
        public const string Result = "Result";
    }

    /// <summary>A menet kimenete a jelenetek között; statikus, mert a jelenetváltás mindent eldob.</summary>
    public static class GameSession
    {
        public static bool HasResult;
        public static InterrogationOutcome Outcome;
        public static string CaseTitle;
        public static string SuspectName;
        public static int Rounds;
        public static int FinalStress;

        public static void Record(InterrogationOutcome outcome, string caseTitle, string suspectName, int rounds, int finalStress)
        {
            HasResult = true;
            Outcome = outcome;
            CaseTitle = caseTitle;
            SuspectName = suspectName;
            Rounds = rounds;
            FinalStress = finalStress;
        }
    }

    /// <summary>
    /// Jelenetváltó segéd. Az „Újra" / „Főmenü" az <c>Interrogation</c> jelenetet tölti be frissen,
    /// így a játékállapot tiszta. A <c>MainMenu</c> / <c>Result</c> jelenet a régi, önálló mód.
    /// </summary>
    public static class GameFlow
    {
        /// <summary>A következő betöltés a menüt átugorva a bevezetővel indul.</summary>
        public static bool SkipMenuOnce;

        public static void RestartInterrogation(bool skipMenu)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SkipMenuOnce = skipMenu;
            ScreenFader.StartBlack = true;
            SceneManager.LoadScene(SceneNames.Interrogation);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => SkipMenuOnce = false;

        public static void LoadMainMenu() => SceneManager.LoadScene(SceneNames.MainMenu);
        public static void LoadInterrogation() => SceneManager.LoadScene(SceneNames.Interrogation);
        public static void LoadResult() => SceneManager.LoadScene(SceneNames.Result);
    }
}
