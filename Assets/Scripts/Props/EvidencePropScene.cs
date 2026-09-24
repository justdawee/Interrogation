using System.Collections;
using UnityEngine;

namespace Interrogation.Props
{
    /// <summary>
    /// Egy bizonyíték tárgy-jelenete: a <see cref="Present"/> kijátssza, a <see cref="Restore"/>
    /// visszaanimálja az induláskor rögzített helyére. Az időzítést az <see cref="EvidencePropDirector"/> adja.
    /// </summary>
    public abstract class EvidencePropScene : MonoBehaviour
    {
        [Tooltip("A bizonyíték (ContradictionPair) fact_id-ja, pl. 'fact_safe'.")]
        [SerializeField] private string evidenceFactId;

        public string EvidenceFactId => evidenceFactId;
        public bool IsPresented { get; protected set; }

        public abstract IEnumerator Present(PropStage stage);
        public abstract IEnumerator Restore(PropStage stage);
    }
}
