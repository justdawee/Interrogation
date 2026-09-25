using System.Threading.Tasks;

namespace Interrogation.Llm
{
    /// <summary>
    /// Bemelegítő hívás a menet előtt: ellenőrzi a kulcsot és a kapcsolatot, hogy az első
    /// valódi kör ne legyen lassabb, és a hiányzó kulcs még a játék előtt kiderüljön.
    /// </summary>
    public static class LlmWarmup
    {
        private const string SystemPrompt =
            "Kapcsolat-ellenőrzés. Válaszolj a sémának megfelelő JSON-nal: dialogue=\"ok\", emotion=calm, " +
            "revealed_fact_id=null, contradiction.detected=false, confession=false, stress_delta=0.";

        public static Task<LlmResult> RunAsync(LlmSettings settings) =>
            new LlmClient(settings).AskAsync(SystemPrompt, "ping");

        public static bool HasApiKey(LlmSettings settings) =>
            !string.IsNullOrEmpty(ApiKeyProvider.Resolve(settings.apiKeyEnvVar, out _));
    }
}
