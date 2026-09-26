using System;
using Interrogation.Game;
using Interrogation.UI;
using UnityEngine;

namespace Interrogation.Flow
{
    /// <summary>
    /// Szünet (Esc): megállítja az időt és a hangot. A futó LLM-hívás a háttérben folytatódik; a válasz
    /// a folytatás után jelenik meg, mert a felirat a skálázott időt követi.
    /// </summary>
    public sealed class PauseController : MonoBehaviour
    {
        [SerializeField] private GameDirector director;
        [SerializeField] private InterrogationManager manager;
        [SerializeField] private InterrogationHUD hud;
        [SerializeField] private PauseMenuView view;

        public bool IsPaused { get; private set; }

        public event Action MainMenuRequested;

        private void OnEnable()
        {
            if (hud != null) hud.PauseRequested += Pause;
            if (view == null) return;
            view.ResumeRequested += Resume;
            view.GiveUpRequested += GiveUp;
            view.MainMenuRequested += ToMainMenu;
        }

        private void OnDisable()
        {
            if (hud != null) hud.PauseRequested -= Pause;
            if (view != null)
            {
                view.ResumeRequested -= Resume;
                view.GiveUpRequested -= GiveUp;
                view.MainMenuRequested -= ToMainMenu;
            }
            if (IsPaused) SetTimeFrozen(false);
        }

        public void Pause()
        {
            if (IsPaused || director == null || !director.CanPause) return;
            IsPaused = true;
            SetTimeFrozen(true);
            UiSoundEvents.Raise(UiSound.Pause);
            if (hud != null) hud.InputBlocked = true;
            view?.Show(() => manager != null && manager.CanAbandon);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            SetTimeFrozen(false);
            view?.Hide();
            if (hud != null) hud.InputBlocked = false;
        }

        private void GiveUp()
        {
            if (manager == null || !manager.CanAbandon) return;
            Resume();
            if (hud != null) hud.InputBlocked = true;
            manager.Abandon(); // a rendező a GameEnded eseményre játssza le a befejezést
        }

        private void ToMainMenu()
        {
            view?.Hide();
            MainMenuRequested?.Invoke();
        }

        private static void SetTimeFrozen(bool frozen)
        {
            Time.timeScale = frozen ? 0f : 1f;
            AudioListener.pause = frozen;
        }
    }
}
