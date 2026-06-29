using System;

namespace Interrogation.Llm
{
    /// <summary>
    /// Az LLM-hívás beállításai egy helyen, hogy a modell egy sorban cserélhető legyen.
    /// Az API-kulcs nem itt van, hanem környezeti változóban.
    /// </summary>
    [Serializable]
    public class LlmSettings
    {
        // Alternatíva: "gpt-6-sol" (erősebb érvelés). A jelenetbe mentett érték felülírja ezt.
        public string model = "gpt-6-luna";

        public string endpoint = "https://api.openai.com/v1/chat/completions";

        // "none" | "low" | "medium" | "high"; üresen a paraméter nem kerül a kérésbe.
        public string reasoningEffort = "low";

        // Reasoning-modellnél a gondolkodás is ebből fogy: 300-nál a hosszabb promptokon
        // üres maradt a válasz, ezért bőséges a plafon (a költséget a tényleges fogyás adja).
        public int maxCompletionTokens = 1200;

        public int timeoutSeconds = 30;

        public int maxRetries = 1;

        public string apiKeyEnvVar = "OPENAI_API_KEY";
    }
}
