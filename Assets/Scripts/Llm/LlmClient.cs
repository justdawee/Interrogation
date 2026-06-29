using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Interrogation.Llm
{
    /// <summary>Egy LLM-hívás eredménye, mérési adatokkal.</summary>
    public sealed class LlmResult
    {
        public SuspectReply Reply;       // mindig kitöltött, hiba esetén a fallback
        public bool Success;
        public bool UsedFallback;
        public int Attempts;
        public long LatencyMs;
        public int PromptTokens;
        public int CompletionTokens;
        public int PromptChars;          // a prompt tömörségének mérőszáma
        public string Error;
    }

    /// <summary>
    /// A „Mind" réteg: async Chat Completions hívás, JSON-validáció, retry és fallback.
    /// Játékállapotot nem ír, csak validált <see cref="SuspectReply"/>-t ad vissza.
    /// </summary>
    public sealed class LlmClient
    {
        private readonly LlmSettings _settings;
        private readonly string _apiKey;

        public bool HasApiKey => !string.IsNullOrEmpty(_apiKey);

        public ApiKeySource KeySource { get; }

        public LlmClient(LlmSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _apiKey = ApiKeyProvider.Resolve(_settings.apiKeyEnvVar, out var source);
            KeySource = source;
        }

        public async Task<LlmResult> AskAsync(string systemPrompt, string userMessage)
        {
            var result = new LlmResult
            {
                Reply = SuspectReply.Fallback(),
                UsedFallback = true,
                PromptChars = (systemPrompt?.Length ?? 0) + (userMessage?.Length ?? 0)
            };

            if (!HasApiKey)
            {
                result.Error = $"Nincs API-kulcs. Forrás keresve: env ('{_settings.apiKeyEnvVar}' " +
                               "process/User/Machine) és projekt-helyi .env. Állítsd be User-scope " +
                               "env-változóként, VAGY tegyél egy .env fájlt a projekt gyökerébe " +
                               $"'{_settings.apiKeyEnvVar}=sk-...' sorral.";
                Debug.LogError($"[LlmClient] {result.Error}");
                return result;
            }

            var body = LlmRequestBuilder.BuildBody(_settings, systemPrompt, userMessage);
            var sw = Stopwatch.StartNew();

            for (int attempt = 1; attempt <= _settings.maxRetries + 1; attempt++)
            {
                result.Attempts = attempt;
                try
                {
                    var http = await SendRequestAsync(body);
                    CaptureUsage(http, result);

                    var content = LlmResponseParser.ExtractContent(http);
                    result.Reply = LlmResponseParser.Parse(content);
                    result.Success = true;
                    result.UsedFallback = false;
                    result.Error = null;
                    break;
                }
                catch (Exception e)
                {
                    result.Error = e.Message;
                    Debug.LogWarning($"[LlmClient] {attempt}. próbálkozás sikertelen: {e.Message}");
                }
            }

            sw.Stop();
            result.LatencyMs = sw.ElapsedMilliseconds;
            return result;
        }

        private async Task<string> SendRequestAsync(string body)
        {
            using var req = new UnityWebRequest(_settings.endpoint, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = _settings.timeoutSeconds;
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + _apiKey);

            var tcs = new TaskCompletionSource<bool>();
            var op = req.SendWebRequest();
            op.completed += _ => tcs.TrySetResult(true);
            await tcs.Task; // a fő szálon folytatódik, de nem blokkol

            if (req.result != UnityWebRequest.Result.Success)
            {
                var detail = ExtractApiError(req.downloadHandler?.text);
                throw new Exception($"HTTP hiba ({(int)req.responseCode}): {req.error}. {detail}");
            }

            return req.downloadHandler.text;
        }

        private static void CaptureUsage(string httpBody, LlmResult result)
        {
            try
            {
                var usage = JObject.Parse(httpBody)["usage"];
                if (usage == null) return;
                result.PromptTokens = usage["prompt_tokens"]?.Value<int>() ?? 0;
                result.CompletionTokens = usage["completion_tokens"]?.Value<int>() ?? 0;
            }
            catch { /* a token-adat opcionális */ }
        }

        private static string ExtractApiError(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return string.Empty;
            try
            {
                var msg = JObject.Parse(body)["error"]?["message"]?.Value<string>();
                return string.IsNullOrEmpty(msg) ? string.Empty : $"API: {msg}";
            }
            catch { return string.Empty; }
        }
    }
}
