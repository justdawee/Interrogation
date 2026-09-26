using Interrogation.UI;
using UnityEngine;

namespace Interrogation.Audio
{
    /// <summary>
    /// A felület hangjai. Szünetet figyelmen kívül hagyó lejátszót használ, és a sűrű jelzéseket
    /// (rámutatás, gépelés) ritkítja.
    /// </summary>
    public sealed class UiAudio : MonoBehaviour
    {
        [SerializeField] private AudioLibrary library;
        [Tooltip("Szünet alatt is szóló lejátszó (ignoreListenerPause).")]
        [SerializeField] private SoundPlayer player;
        [SerializeField] private float minRepeatSeconds = 0.035f;

        private float _lastTime = -1f;
        private UiSound _lastSound;

        private void OnEnable() => UiSoundEvents.Raised += OnSound;
        private void OnDisable() => UiSoundEvents.Raised -= OnSound;

        private void OnSound(UiSound sound)
        {
            if (library == null || player == null) return;
            var now = Time.unscaledTime;
            if (sound == _lastSound && now - _lastTime < minRepeatSeconds) return;
            _lastSound = sound;
            _lastTime = now;

            player.Play(CueFor(sound), GameSettings.Volume(SoundChannel.Sfx));
        }

        private SoundCue CueFor(UiSound sound)
        {
            switch (sound)
            {
                case UiSound.Hover: return library.uiHover;
                case UiSound.Click: return library.uiClick;
                case UiSound.Back: return library.uiBack;
                case UiSound.Error: return library.uiError;
                case UiSound.Send: return library.uiSend;
                case UiSound.Type: return library.typewriterKey;
                case UiSound.Open: return library.uiOpen;
                case UiSound.Close: return library.uiClose;
                case UiSound.Page: return library.paper;
                case UiSound.Pause: return library.uiPause;
                default: return null;
            }
        }
    }
}
