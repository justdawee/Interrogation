using Interrogation.Rendering;
using Interrogation.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Interrogation.Props
{
    /// <summary>
    /// Izzó körvonal az egér alatti bizonyíték-tárgyon (vagy az aktában rámutatott kártya tárgyán),
    /// de csak ha a bizonyíték éppen választható: a körvonal azt is jelzi, hogy „most lehet".
    /// </summary>
    public sealed class EvidenceHoverHighlighter : MonoBehaviour
    {
        [SerializeField] private EvidencePropDirector director;
        [SerializeField] private CaseFilePanel caseFile;
        [Tooltip("A körvonal színe (a renderer-feature HDR-szorzóval izzítja).")]
        [SerializeField] private Color color = NoirPalette.Amber;
        [Tooltip("Be-/kiúsztatás sebessége (1/mp).")]
        [SerializeField] private float fadeSpeed = 8f;

        private EvidencePropScene _shown;
        private EvidencePropScene _hovered;
        private Renderer[] _renderers = new Renderer[0];
        private float _intensity;

        private void OnDisable()
        {
            _shown = null;
            _intensity = 0f;
            OutlineTargets.Clear();
        }

        private void Update()
        {
            var wanted = director != null && director.CanSelect ? Hovered() : null;

            // A nyitott akta kártyáinál a felület rámutatás-hangja szól, itt a tárgyé.
            if (wanted != _hovered)
            {
                if (wanted != null && (caseFile == null || !caseFile.IsOpen)) UiSoundEvents.Raise(UiSound.Hover);
                _hovered = wanted;
            }

            // Célváltáskor előbb kiúsztat, csak utána vált át a másik tárgyra.
            if (wanted != _shown && _intensity <= 0f)
            {
                _shown = wanted;
                _renderers = wanted != null ? wanted.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            }

            var target = wanted != null && wanted == _shown ? 1f : 0f;
            _intensity = Mathf.MoveTowards(_intensity, target, fadeSpeed * Time.unscaledDeltaTime);

            if (_shown == null || _intensity <= 0f) OutlineTargets.Clear();
            else OutlineTargets.Set(_renderers, color, Mathf.SmoothStep(0f, 1f, _intensity));
        }

        // A már kijátszott tárgy nem kap körvonalat.
        private EvidencePropScene Hovered()
        {
            EvidencePropScene scene;
            if (caseFile != null && caseFile.IsOpen)
                scene = director.SceneFor(caseFile.HoveredEvidenceId);
            else
            {
                var mouse = Mouse.current;
                scene = mouse != null ? director.PickAt(mouse.position.ReadValue()) : null;
            }
            return scene != null && !scene.IsPresented ? scene : null;
        }
    }
}
