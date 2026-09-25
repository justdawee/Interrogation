using System;
using System.Globalization;
using System.Text;
using Interrogation.Game;
using Interrogation.Llm;

namespace Interrogation.Telemetry
{
    /// <summary>Egy mérési sor (LLM-hívás vagy menet-esemény). Oszlopok: <see cref="Header"/>.</summary>
    public sealed class TelemetryRecord
    {
        public const string Header =
            "session,timestamp_utc,kind,round,model,reasoning,latency_ms,ttft_ms,attempts,success,fallback," +
            "prompt_tokens,completion_tokens,prompt_chars,emotion,stress_delta,stress,evidence_id," +
            "contradiction_raw,contradiction_accepted,fact_raw,fact_accepted,exposed_fact,guard_note," +
            "confession,outcome,derail_flags,dialogue_chars,error,stress_points";

        private readonly string[] _cells;

        private TelemetryRecord(string[] cells) => _cells = cells;

        public static TelemetryRecord FromTurn(string session, LlmSettings settings, TurnResult turn)
        {
            var llm = turn.LlmResult ?? new LlmResult();
            var raw = turn.RawReply;
            var reply = turn.Reply;
            return new TelemetryRecord(new[]
            {
                session, Now(), "turn", I(turn.Round), settings?.model, settings?.reasoningEffort,
                L(llm.LatencyMs), string.Empty, I(llm.Attempts), B(llm.Success), B(llm.UsedFallback),
                I(llm.PromptTokens), I(llm.CompletionTokens), I(llm.PromptChars),
                reply?.Emotion.ToString(), I(turn.StressDelta), I(turn.Stress), turn.EvidenceId,
                B(raw != null && raw.Contradiction.Detected), B(reply != null && reply.Contradiction.Detected),
                raw?.RevealedFactId, reply?.RevealedFactId, turn.Guard.ExposedFactId, turn.Guard.Note,
                B(reply != null && reply.Confession), turn.Outcome.ToString(),
                llm.Success ? DerailmentHeuristic.Detect(raw?.Dialogue) : string.Empty,
                I(raw?.Dialogue?.Length ?? 0), llm.Error, I(turn.StressPoints)
            });
        }

        public static TelemetryRecord FromWarmup(string session, LlmSettings settings, LlmResult llm)
        {
            llm ??= new LlmResult();
            return new TelemetryRecord(new[]
            {
                session, Now(), "warmup", "0", settings?.model, settings?.reasoningEffort,
                L(llm.LatencyMs), string.Empty, I(llm.Attempts), B(llm.Success), B(llm.UsedFallback),
                I(llm.PromptTokens), I(llm.CompletionTokens), I(llm.PromptChars),
                "", "", "", "", "", "", "", "", "", "", "", "", "", "", llm.Error, ""
            });
        }

        /// <param name="usedLlmLine">A modell szövege hangzott-e el (hamis = tartalék-vallomás).</param>
        public static TelemetryRecord FromConfession(string session, LlmSettings settings, LlmResult llm, int round,
            bool usedLlmLine)
        {
            llm ??= new LlmResult();
            var reply = llm.Reply;
            return new TelemetryRecord(new[]
            {
                session, Now(), "confession", I(round), settings?.model, settings?.reasoningEffort,
                L(llm.LatencyMs), string.Empty, I(llm.Attempts), B(llm.Success), B(llm.UsedFallback),
                I(llm.PromptTokens), I(llm.CompletionTokens), I(llm.PromptChars),
                reply?.Emotion.ToString(), "", "", "", "", "", "", "", "", usedLlmLine ? "" : "tartalék-vallomás",
                B(reply != null && reply.Confession), "Confessed",
                llm.Success ? DerailmentHeuristic.Detect(reply?.Dialogue) : string.Empty,
                I(reply?.Dialogue?.Length ?? 0), llm.Error, ""
            });
        }

        public static TelemetryRecord FromEnd(string session, LlmSettings settings, InterrogationOutcome outcome, int rounds, int stress)
        {
            return new TelemetryRecord(new[]
            {
                session, Now(), "end", I(rounds), settings?.model, settings?.reasoningEffort,
                "", "", "", "", "", "", "", "", "", "", I(stress), "", "", "", "", "", "", "", "",
                outcome.ToString(), "", "", "", ""
            });
        }

        public string ToCsvLine()
        {
            var sb = new StringBuilder(256);
            for (int i = 0; i < _cells.Length; i++)
            {
                if (i > 0) sb.Append(',');
                AppendCell(sb, _cells[i]);
            }
            return sb.ToString();
        }

        // RFC 4180 szerinti idézés.
        private static void AppendCell(StringBuilder sb, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            var needsQuotes = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            if (!needsQuotes) { sb.Append(value); return; }
            sb.Append('"').Append(value.Replace("\"", "\"\"")).Append('"');
        }

        private static string Now() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);
        private static string L(long v) => v.ToString(CultureInfo.InvariantCulture);
        private static string B(bool v) => v ? "1" : "0";
    }
}
