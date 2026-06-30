using UnityEngine;

namespace Interrogation.UI
{
    public sealed class MainMenuUI : MonoBehaviour
    {
        public void OnStart()
        {
            GameFlow.LoadInterrogation();
        }

        public void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
