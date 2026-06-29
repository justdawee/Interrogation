using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Interrogation.Llm
{
    /// <summary>
    /// Az LLM nyers válaszát validálja és <see cref="SuspectReply"/>-ra alakítja.
    /// Hibás vagy hiányos JSON esetén <see cref="LlmParseException"/>-t dob, erre a kliens újrapróbál.
    /// </summary>
    public static class LlmResponseParser
    {
        public static string ExtractContent(string rawHttpBody)
        {
            if (string.IsNullOrWhiteSpace(rawHttpBody))
                throw new LlmParseException("Üres HTTP-válasz.");

            JObject root;
            try { root = JObject.Parse(rawHttpBody); }
            catch (JsonException e) { throw new LlmParseException("A HTTP-válasz nem érvényes JSON.", e); }

            var message = root["choices"]?[0]?["message"];
            if (message == null)
                throw new LlmParseException("Hiányzik a 'choices[0].message' a válaszból.");

            var refusal = message["refusal"]?.Type == JTokenType.String
                ? message["refusal"].Value<string>()
                : null;
            if (!string.IsNullOrEmpty(refusal))
                throw new LlmParseException($"A modell elutasította a kérést: {refusal}");

            var content = message["content"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(content))
            {
                // Reasoning-modellnél a gondolkodás is a max_completion_tokens-ből fogy: ha elfogy,
                // a látható válasz üres marad (finish_reason = "length").
                var finish = root["choices"]?[0]?["finish_reason"]?.Value<string>();
                throw new LlmParseException(finish == "length"
                    ? "Üres 'content': a token-plafon elfogyott (finish_reason=length) — emeld a maxCompletionTokens-t."
                    : "Üres 'content' a model-üzenetben.");
            }

            return content;
        }

        public static SuspectReply Parse(string contentJson)
        {
            JObject o;
            try { o = JObject.Parse(contentJson); }
            catch (JsonException e) { throw new LlmParseException("A model-tartalom nem érvényes JSON.", e); }

            var dialogue = RequireString(o, "dialogue");
            var emotion = ParseEmotion(RequireString(o, "emotion"));

            var revealedFactId = OptionalString(o, "revealed_fact_id");

            var contraTok = o["contradiction"] as JObject
                ?? throw new LlmParseException("Hiányzik vagy hibás a 'contradiction' objektum.");
            var contradiction = new Contradiction
            {
                Detected = RequireBool(contraTok, "detected"),
                WithStatementId = OptionalString(contraTok, "with_statement_id")
            };

            var confession = RequireBool(o, "confession");
            var stressDelta = RequireInt(o, "stress_delta");

            return new SuspectReply(dialogue, emotion, revealedFactId, contradiction, confession, stressDelta);
        }

        private static string RequireString(JObject o, string key)
        {
            var t = o[key];
            if (t == null || t.Type != JTokenType.String)
                throw new LlmParseException($"Hiányzó vagy nem string mező: '{key}'.");
            var s = t.Value<string>();
            if (string.IsNullOrWhiteSpace(s))
                throw new LlmParseException($"Üres kötelező mező: '{key}'.");
            return s;
        }

        private static string OptionalString(JObject o, string key)
        {
            var t = o[key];
            if (t == null || t.Type == JTokenType.Null) return null;
            if (t.Type != JTokenType.String)
                throw new LlmParseException($"A(z) '{key}' mező string vagy null kell legyen.");
            var s = t.Value<string>();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        private static bool RequireBool(JObject o, string key)
        {
            var t = o[key];
            if (t == null || t.Type != JTokenType.Boolean)
                throw new LlmParseException($"Hiányzó vagy nem bool mező: '{key}'.");
            return t.Value<bool>();
        }

        private static int RequireInt(JObject o, string key)
        {
            var t = o[key];
            if (t == null || (t.Type != JTokenType.Integer && t.Type != JTokenType.Float))
                throw new LlmParseException($"Hiányzó vagy nem szám mező: '{key}'.");
            return t.Value<int>();
        }

        private static SuspectEmotion ParseEmotion(string raw)
        {
            switch (raw.Trim().ToLowerInvariant())
            {
                case "calm": return SuspectEmotion.Calm;
                case "nervous": return SuspectEmotion.Nervous;
                case "defensive": return SuspectEmotion.Defensive;
                case "hostile": return SuspectEmotion.Hostile;
                case "breaking": return SuspectEmotion.Breaking;
                default:
                    throw new LlmParseException($"Ismeretlen 'emotion' érték: '{raw}'.");
            }
        }
    }

    public sealed class LlmParseException : Exception
    {
        public LlmParseException(string message, Exception inner = null) : base(message, inner) { }
    }
}
