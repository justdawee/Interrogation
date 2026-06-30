using System;
using UnityEngine;

namespace Interrogation.Memory
{
    /// <summary>
    /// A gyanúsított egy hamis állítása és a bizonyíték, amellyel a detektív cáfolhatja.
    /// </summary>
    [Serializable]
    public sealed class ContradictionPair
    {
        [Tooltip("Annak a ténynek az id-ja, amit ez a bizonyíték cáfol (pl. 'fact_alibi').")]
        public string factId;

        [Tooltip("A detektív bizonyítékának rövid neve (pl. 'telefonnapló').")]
        public string evidenceLabel;

        [TextArea(2, 4)]
        [Tooltip("Az ellentmondás leírása: mit cáfol és hogyan.")]
        public string description;
    }
}
