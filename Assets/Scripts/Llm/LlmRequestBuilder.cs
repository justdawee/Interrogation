using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Interrogation.Llm
{
    /// <summary>
    /// A Chat Completions kérés törzse Structured Outputs (strict json_schema) kényszerrel,
    /// így a modell kimenete séma-szinten követi a JSON-kontraktust.
    /// </summary>
    public static class LlmRequestBuilder
    {
        public static string BuildBody(LlmSettings s, string systemPrompt, string userMessage)
        {
            var body = new JObject
            {
                ["model"] = s.model,
                ["messages"] = new JArray
                {
                    new JObject { ["role"] = "system", ["content"] = systemPrompt },
                    new JObject { ["role"] = "user", ["content"] = userMessage }
                },
                ["max_completion_tokens"] = s.maxCompletionTokens,
                ["response_format"] = BuildResponseFormat()
            };

            if (!string.IsNullOrWhiteSpace(s.reasoningEffort))
                body["reasoning_effort"] = s.reasoningEffort.Trim().ToLowerInvariant();

            return body.ToString(Formatting.None);
        }

        // Strict módban minden mező kötelező, ezért a nullable mezők ["string","null"] típusúak.
        private static JObject BuildResponseFormat()
        {
            var schema = new JObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JArray("dialogue", "emotion", "revealed_fact_id", "contradiction", "confession", "stress_delta"),
                ["properties"] = new JObject
                {
                    ["dialogue"] = new JObject { ["type"] = "string" },
                    ["emotion"] = new JObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JArray("calm", "nervous", "defensive", "hostile", "breaking")
                    },
                    ["revealed_fact_id"] = NullableType("string"),
                    ["contradiction"] = new JObject
                    {
                        ["type"] = "object",
                        ["additionalProperties"] = false,
                        ["required"] = new JArray("detected", "with_statement_id"),
                        ["properties"] = new JObject
                        {
                            ["detected"] = new JObject { ["type"] = "boolean" },
                            ["with_statement_id"] = NullableType("string")
                        }
                    },
                    ["confession"] = new JObject { ["type"] = "boolean" },
                    ["stress_delta"] = new JObject { ["type"] = "integer" }
                }
            };

            return new JObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JObject
                {
                    ["name"] = "suspect_reply",
                    ["strict"] = true,
                    ["schema"] = schema
                }
            };
        }

        private static JObject NullableType(string primary) =>
            new JObject { ["type"] = new JArray(primary, "null") };
    }
}
