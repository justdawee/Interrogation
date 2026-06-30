using System;
using UnityEngine;

namespace Interrogation.Memory
{
    /// <summary>
    /// A gyanúsított rejtett tudásának egy ténye; a titkos tények a kicsikarandó igazság.
    /// </summary>
    [Serializable]
    public sealed class FactNode
    {
        [Tooltip("Egyedi azonosító, pl. 'fact_debt'. Erre hivatkozik a JSON revealed_fact_id.")]
        public string id;

        [TextArea(2, 4)]
        [Tooltip("A tény tartalma a gyanúsított nézőpontjából (mit tud / mit titkol).")]
        public string content;

        [Tooltip("Titkos tény-e: ha igaz, a gyanúsított védi, és a felfedése visz a vallomás felé.")]
        public bool isSecret;

        [Tooltip("A tény szerepe a dramaturgiában, pl. 'motívum', 'bizonyíték', 'hamis alibi'.")]
        public string role;

        [Tooltip("Rövid, a detektív szemszögéből írt nyom-felirat a UI-hoz (toast, akta). Üres = a szerep látszik.")]
        public string clueLabel;

        [Tooltip("Kulcsszavak a relevancia-illesztéshez (a detektív kérdéséhez). Kisbetűs, tő alak ajánlott.")]
        public string[] keywords = Array.Empty<string>();
    }
}
