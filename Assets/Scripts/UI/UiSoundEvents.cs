using System;

namespace Interrogation.UI
{
    public enum UiSound
    {
        Hover,      // választás mozgatása a menüben
        Click,      // menüpont aktiválása / érték állítása
        Back,       // visszalépés (Esc)
        Error,      // nem választható menüpont
        Send,       // kérdés elküldése
        Type,       // leütés a kérdés-sorban (írógép-billentyű)
        Open,       // akta / aktalap nyitása
        Close,      // akta / aktalap zárása
        Page,       // fülváltás, bizonyíték csatolása (papírzörgés)
        Pause       // szünet-menü megnyitása
    }

    /// <summary>
    /// A felület hang-eseményeinek gyűjtőpontja; a UI-komponensek hangréteg nélkül is működnek.
    /// </summary>
    public static class UiSoundEvents
    {
        public static event Action<UiSound> Raised;

        public static void Raise(UiSound sound) => Raised?.Invoke(sound);
    }
}
