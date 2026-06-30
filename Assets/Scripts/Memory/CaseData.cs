using System.Collections.Generic;
using UnityEngine;

namespace Interrogation.Memory
{
    /// <summary>
    /// Egy kihallgatási eset adatcsomagja: a gyanúsított perszónája, a rejtett tudásgráf,
    /// az ellentmondás-párok és a játékszabály-paraméterek. Logikát nem tartalmaz.
    /// </summary>
    [CreateAssetMenu(fileName = "case_01", menuName = "Interrogation/Case Data", order = 0)]
    public sealed class CaseData : ScriptableObject
    {
        [Header("Azonosítás")]
        public string caseId = "case_01";
        public string caseTitle = "Az utolsó hajó";

        [Header("Gyanúsított")]
        public string suspectName = "Arthur Hale";

        [TextArea(4, 12)]
        [Tooltip("A gyanúsított stabil perszónája és viselkedési utasítása. Ez a system prompt magja — " +
                 "a tudásgráf és a memória ehhez fűződik hozzá a ContextBuilderben. Tartsd fókuszáltan és " +
                 "rövidnek a derailment ellen [5].")]
        public string personaPrompt =
            "Egy noir bűnügyi nyomozós játék gyanúsítottját játszod. Hideg, számító, eladósodott férfi vagy, " +
            "aki bűnös, de mindvégig tagad. Védekező, néha ideges vagy ellenséges; empátiára enyhén megnyílsz. " +
            "Maradj VÉGIG karakterben: sose lépj ki a szerepből, sose említs AI-t, modellt vagy szabályokat. " +
            "Magyarul, tömören válaszolj (1-3 mondat). Csak akkor vallj be bármit, ha a bizonyítékok " +
            "tényleg sarokba szorítanak.";

        [TextArea(2, 6)]
        [Tooltip("Az eligazítás tényállása (a főmenü után, az aktában) — csak a játékosnak szól, a promptba nem kerül.")]
        public string briefing =
            "Karl Reinhardtot, egy kikötői szállítmányozó cég társtulajdonosát holtan találták az irodájában. " +
            "A széf kifosztva, a helyszín betörésnek látszik. A gyanúsított az üzlettársa.";

        [Tooltip("Előre megírt nyitómondat a menet elején (nincs LLM-hívás, nincs latency).")]
        public string openingLine = "Már mindent elmondtam a kollégáinak, felügyelő.";

        [TextArea(2, 6)]
        [Tooltip("Tartalék vallomás-monológ: ha a nyomás-győzelem után a vallomást kimondató LLM-hívás " +
                 "sikertelen, ez hangzik el (a győzelemről a Game réteg dönt, a szöveg csak hangot ad neki).")]
        public string confessionLine =
            "Rendben... Én voltam. Az adósságok megfojtottak, és Karl rájött, hogy nyúltam a cég pénzéhez. " +
            "Másnap a rendőrségre akart menni. Aznap éjjel a saját pisztolyommal... aztán kifosztottam a széfet, " +
            "hogy betörésnek látsszon.";

        [Header("Tudásgráf (rejtett tudás)")]
        public List<FactNode> facts = new List<FactNode>();

        [Header("Ellentmondás-párok (a detektív bizonyítékai)")]
        public List<ContradictionPair> contradictions = new List<ContradictionPair>();

        [Header("Játékszabály-paraméterek")]
        [Tooltip("Stressz-küszöb a vallomáshoz (a StressMeter klampje 0..ide).")]
        public int stressThreshold = 100;

        [Tooltip("Maximális kérdés-szám; ennek kimerülése vereség.")]
        public int roundLimit = 20;

        [Tooltip("Hány kulcs-ellentmondást kell felfedni a töréshez (a stressz-küszöb mellett).")]
        public int minKeyContradictionsToBreak = 2;

        [Tooltip("Kötelezően felfedendő kulcs-ellentmondás fact_id-ja a győzelemhez (üres = nincs kötelező).")]
        public string requiredContradictionFactId = "fact_alibi";

        [Tooltip("Extra stressz, ha az LLM ellentmondást jelez (contradiction.detected) — a [3] szembesítés-mechanika.")]
        public int contradictionStressBonus = 2;

        [Tooltip("A kör stressz-pontjainak (LLM stress_delta + ellentmondás-bónusz) szorzója a stressz-mérőn. " +
                 "Az LLM skálája (-3..+3) változatlan marad; a türelem-szabály a szorzatlan értéket nézi.")]
        [Min(1)] public int stressMultiplier = 4;

        public KnowledgeGraph BuildKnowledgeGraph() => new KnowledgeGraph(facts);
    }
}
