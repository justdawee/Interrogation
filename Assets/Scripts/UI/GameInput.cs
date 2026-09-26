using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Interrogation.UI
{
    /// <summary>
    /// A játék bemenete egy helyen, az <c>InputSystem_Actions</c> asset <c>Interrogation</c> és <c>UI</c>
    /// térképéből. Hiányzó akciónál a megadott billentyű a tartalék, így az irányítás sosem esik ki.
    /// </summary>
    public static class GameInput
    {
        public const string GameMap = "Interrogation";
        public const string UiMap = "UI";

        private static readonly Dictionary<string, InputAction> Cache = new Dictionary<string, InputAction>();
        private static int _navFrame = -1;
        private static Vector2Int _navPrev;
        private static Vector2Int _navNow;

        public static bool Pause => Pressed(GameMap, "Pause", Key.Escape);
        public static bool CaseFile => Pressed(GameMap, "CaseFile", Key.Tab);
        public static bool SkipLine => Pressed(GameMap, "SkipLine", Key.Space);
        public static bool Focus => Pressed(GameMap, "Focus", Key.Enter) || Pressed(GameMap, "Focus", Key.NumpadEnter);

        public static bool Evidence(int oneBased) =>
            Pressed(GameMap, "Evidence" + oneBased, Key.Digit1 + (oneBased - 1));

        public static bool Submit => Pressed(UiMap, "Submit", Key.Enter);
        public static bool Cancel => Pressed(UiMap, "Cancel", Key.Escape);

        /// <summary>Élvezérelt: tartott iránynál csak egyszer sül el. x: bal −1 / jobb +1, y: fel +1 / le −1.</summary>
        public static Vector2Int NavigatePressed
        {
            get
            {
                UpdateNavigation();
                return _navNow != _navPrev ? _navNow : Vector2Int.zero;
            }
        }

        private static void UpdateNavigation()
        {
            if (_navFrame == Time.frameCount) return;
            _navFrame = Time.frameCount;
            _navPrev = _navNow;

            var action = Find(UiMap, "Navigate");
            var v = action != null ? action.ReadValue<Vector2>() : KeyboardVector();
            _navNow = new Vector2Int(Axis(v.x), Axis(v.y));
            // egyszerre csak egy tengely, a függőleges elsőbbséggel
            if (_navNow.y != 0) _navNow.x = 0;
        }

        private static int Axis(float value) => value > 0.5f ? 1 : value < -0.5f ? -1 : 0;

        private static Vector2 KeyboardVector()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            var x = (kb.rightArrowKey.isPressed ? 1 : 0) - (kb.leftArrowKey.isPressed ? 1 : 0);
            var y = (kb.upArrowKey.isPressed ? 1 : 0) - (kb.downArrowKey.isPressed ? 1 : 0);
            return new Vector2(x, y);
        }

        private static bool Pressed(string map, string action, Key fallback)
        {
            var a = Find(map, action);
            if (a != null) return a.WasPressedThisFrame();
            var kb = Keyboard.current;
            return kb != null && kb[fallback].wasPressedThisFrame;
        }

        private static InputAction Find(string map, string action)
        {
            var key = map + "/" + action;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var found = InputSystem.actions != null ? InputSystem.actions.FindAction(key, throwIfNotFound: false) : null;
            if (found != null && !found.enabled) found.Enable();
            Cache[key] = found;
            return found;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            _navFrame = -1;
            _navPrev = _navNow = Vector2Int.zero;
        }
    }
}
