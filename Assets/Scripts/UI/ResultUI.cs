using Interrogation.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Interrogation.UI
{
    /// <summary>Az önálló eredmény-jelenet: a <see cref="GameSession"/>-ből olvassa a kimenetet.</summary>
    public sealed class ResultUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Image accent;

        private void Start()
        {
            if (!GameSession.HasResult)
            {
                Render("—", "Nincs lezárt menet.", NoirPalette.TextMuted);
                return;
            }

            switch (GameSession.Outcome)
            {
                case InterrogationOutcome.Confessed:
                    Render("VALLOMÁS", $"{GameSession.SuspectName} megtört és bevallotta a tettet.\n" +
                                       $"Az ügy lezárva — {GameSession.CaseTitle}.", NoirPalette.Amber);
                    break;
                case InterrogationOutcome.LawyeredUp:
                    Render("ÜGYVÉDET KÉRT", $"{GameSession.SuspectName} elzárkózott és ügyvédet követelt.\n" +
                                            "A kihallgatás véget ért — nincs vallomás.", NoirPalette.StressHigh);
                    break;
                case InterrogationOutcome.OutOfQuestions:
                    Render("KIFUTOTT AZ IDŐ", "Elfogytak a kérdések, a gyanúsított nem tört meg.\n" +
                                              "Az ügy nyitva marad.", NoirPalette.ColdBlue);
                    break;
                default:
                    Render("VÉGE", "A menet véget ért.", NoirPalette.TextMuted);
                    break;
            }
        }

        private void Render(string title, string body, Color color)
        {
            if (titleText != null) { titleText.text = title; titleText.color = color; }
            if (bodyText != null) bodyText.text = body;
            if (accent != null) accent.color = color;
        }

        public void OnRetry() => GameFlow.LoadInterrogation();
        public void OnMenu() => GameFlow.LoadMainMenu();
    }
}
