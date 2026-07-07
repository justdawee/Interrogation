using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
#endif

namespace Interrogation.Utility
{
    /// <summary>
    /// Csak szerkesztőben: teljes képernyős Game nézet Play-induláskor, billentyűvel váltható.
    /// A buildben is létezik (ne legyen „missing script"), de ott nem csinál semmit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FullscreenHotkeyHandler : MonoBehaviour
    {
        [Header("Fullscreen Game View (Editor only)")]
        [Tooltip("Ha igaz, a játék indulásakor automatikusan fullscreen Game View-t nyit.")]
        [SerializeField] bool makeFullscreenAtStart = true;

        [Tooltip("Ez a billentyű váltogatja a fullscreen nézetet játék közben. Szabadon átállítható.")]
        [SerializeField] Key toggleKey = Key.Backslash;

#if UNITY_EDITOR
        void Start()
        {
            if (makeFullscreenAtStart)
            {
                FullscreenGameView.Toggle();
            }
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard[toggleKey].wasPressedThisFrame)
            {
                FullscreenGameView.Toggle();
            }
        }
#else
        void Awake() => enabled = false;
#endif
    }

#if UNITY_EDITOR
    // Forrás: https://gist.github.com/fnuecke/d4275087cc7969257eae0f939fac3d2f
    // Javítás: újrafordítás után nem ragad be a fullscreen nézet.
    static class FullscreenGameView
    {
        static readonly Type GameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
        static readonly PropertyInfo ShowToolbarProperty = GameViewType?.GetProperty("showToolbar", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly object False = false; // egyszer boxolva

        static EditorWindow instance;

        // Újrafordításkor bezárjuk a popupot, különben nem lehet kilépni a fullscreenből.
        static FullscreenGameView() { AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload; }
        static void OnBeforeAssemblyReload() { if (instance != null) { instance.Close(); instance = null; } }

        [MenuItem("Window/General/Game (Fullscreen) %#&2", priority = 2)]
        public static void Toggle()
        {
            if (GameViewType == null)
            {
                Debug.LogError("[FullscreenGameView] A GameView típus nem található.");
                return;
            }

            if (ShowToolbarProperty == null)
            {
                Debug.LogWarning("[FullscreenGameView] A GameView.showToolbar property nem található.");
            }

            if (instance != null)
            {
                instance.Close();
                instance = null;
                return;
            }

            instance = (EditorWindow)ScriptableObject.CreateInstance(GameViewType);
            ShowToolbarProperty?.SetValue(instance, False);

            // DPI-skálázás nélkül a popup elcsúszik, és nem fedi le a tálcát.
            var scale = EditorGUIUtility.pixelsPerPoint; // pl. 1.5 @150%
            var desktopResolution = new Vector2(
                Screen.currentResolution.width / scale,
                Screen.currentResolution.height / scale);
            var fullscreenRect = new Rect(Vector2.zero, desktopResolution);

            instance.ShowPopup();
            instance.position = fullscreenRect;
            // Rögzítjük a méretet, hogy a Windows ne hagyjon rést a taskbarnak.
            instance.minSize = instance.maxSize = desktopResolution;
            instance.position = fullscreenRect;
            instance.Focus();
        }
    }
#endif
}
