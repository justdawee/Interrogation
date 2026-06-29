using System.Threading.Tasks;
using UnityEngine;

namespace Interrogation.Llm
{
    /// <summary>
    /// Izolált LLM-teszt: egy rögzített kérdést küld, és a konzolra írja a választ
    /// a válaszidővel és a tokenszámmal együtt.
    /// </summary>
    public sealed class LlmClientTester : MonoBehaviour
    {
        [Header("Beállítások")]
        public LlmSettings settings = new LlmSettings();

        [Tooltip("Hardcode-olt detektív-kérdés az izolált teszthez.")]
        [TextArea] public string testQuestion = "Hol volt tegnap este tíz óra körül?";

        [Tooltip("Ha igaz, Play-induláskor automatikusan lefuttatja a tesztet.")]
        public bool runOnStart = true;

        // A sémát a Structured Outputs kényszeríti ki, ezért a prompt csak a karakterre fókuszál.
        private const string SystemPrompt =
            "Egy noir bűnügyi nyomozós játék gyanúsítottját játszod: Arthur Hale, " +
            "akit egy éjszakai betörés és egy ember halála ügyében hallgatnak ki. " +
            "Bűnös vagy, de tagadsz; védekező, ideges, néha ellenséges. Maradj végig " +
            "karakterben, sose lépj ki a szerepből, sose említs AI-t vagy szabályokat. " +
            "Röviden válaszolj (1-3 mondat), magyarul. A 'stress_delta' a kérdés " +
            "fenyegető voltát tükrözze (-3..+3); 'confession' csak akkor true, ha " +
            "tényleg beismered a tettet.";

        private async void Start()
        {
            if (runOnStart) await RunTest();
        }

        [ContextMenu("Run Interrogation Test")]
        public void RunTestMenu()
        {
            _ = RunTest();
        }

        // Edit módú futtatáshoz: statikus állapot, amely a hívások között is megmarad.
        public static LlmResult LastResult;
        public static bool Running;
        public static string LastError;

        public static void BeginStaticTest(LlmSettings settings, string question)
        {
            Running = true;
            LastResult = null;
            LastError = null;
            _ = RunStatic(settings, question);
        }

        private static async Task RunStatic(LlmSettings settings, string question)
        {
            try
            {
                var client = new LlmClient(settings);
                LastResult = await client.AskAsync(SystemPrompt, question);
            }
            catch (System.Exception e)
            {
                LastError = e.ToString();
            }
            finally
            {
                Running = false;
            }
        }

        public async Task RunTest()
        {
            Debug.Log($"[Tester] Modell: {settings.model} | Kérdés: \"{testQuestion}\"");

            var client = new LlmClient(settings);
            var result = await client.AskAsync(SystemPrompt, testQuestion);

            if (result.Success)
            {
                Debug.Log(
                    $"[Tester] ✅ Siker ({result.Attempts}. próbálkozás, {result.LatencyMs} ms, " +
                    $"prompt={result.PromptTokens} / completion={result.CompletionTokens} token)\n" +
                    $"{result.Reply}");
            }
            else
            {
                Debug.LogError(
                    $"[Tester] ❌ Fallback ({result.Attempts} próbálkozás, {result.LatencyMs} ms). " +
                    $"Ok: {result.Error}\nMegjelenített sor: \"{result.Reply.Dialogue}\"");
            }
        }
    }
}
