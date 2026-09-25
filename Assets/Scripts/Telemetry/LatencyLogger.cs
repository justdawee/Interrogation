using System;
using System.IO;
using System.Text;
using Interrogation.Game;
using Interrogation.Llm;
using UnityEngine;

namespace Interrogation.Telemetry
{
    /// <summary>
    /// Mérési napló: minden LLM-hívásról és a menet végéről egy CSV-sort ír. Menetenként új fájl,
    /// soronként azonnal lemezre, így egy megszakadt menet adata is megmarad.
    /// Hely: szerkesztőben <c>Logs/Telemetry/</c>, buildben <c>persistentDataPath/Telemetry/</c>.
    /// </summary>
    public sealed class LatencyLogger : MonoBehaviour
    {
        [SerializeField] private InterrogationManager manager;
        [Tooltip("Kikapcsolva nem ír fájlt (pl. bemutatón).")]
        [SerializeField] private bool writeToDisk = true;

        public string SessionId { get; private set; }
        public string FilePath { get; private set; }

        private void Awake()
        {
            if (manager == null) manager = FindAnyObjectByType<InterrogationManager>();
            SessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }

        private void OnEnable()
        {
            if (manager == null) return;
            manager.TurnCompleted += OnTurnCompleted;
            manager.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            if (manager == null) return;
            manager.TurnCompleted -= OnTurnCompleted;
            manager.GameEnded -= OnGameEnded;
        }

        public void LogWarmup(LlmResult result) => Write(TelemetryRecord.FromWarmup(SessionId, Settings, result));

        public void LogConfession(LlmResult result, bool usedLlmLine)
        {
            var round = manager != null && manager.Memory != null ? manager.Memory.Round : 0;
            Write(TelemetryRecord.FromConfession(SessionId, Settings, result, round, usedLlmLine));
        }

        private void OnTurnCompleted(TurnResult turn) => Write(TelemetryRecord.FromTurn(SessionId, Settings, turn));

        private void OnGameEnded(InterrogationOutcome outcome)
        {
            var rounds = manager.Memory != null ? manager.Memory.Round : 0;
            var stress = manager.Stress != null ? manager.Stress.Current : 0;
            Write(TelemetryRecord.FromEnd(SessionId, Settings, outcome, rounds, stress));
        }

        private LlmSettings Settings => manager != null ? manager.settings : null;

        private void Write(TelemetryRecord record)
        {
            if (!writeToDisk || record == null) return;
            try
            {
                if (FilePath == null) FilePath = CreateSessionFile();
                File.AppendAllText(FilePath, record.ToCsvLine() + "\n", new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                // A mérés sosem akaszthatja meg a játékot.
                Debug.LogWarning($"[LatencyLogger] A mérési sor nem írható: {e.Message}");
                writeToDisk = false;
            }
        }

        private string CreateSessionFile()
        {
            var dir = Path.Combine(LogRoot(), "Telemetry");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"session_{SessionId}.csv");
            // BOM, hogy a táblázatkezelő az ékezeteket helyesen olvassa.
            File.WriteAllText(path, TelemetryRecord.Header + "\n", new UTF8Encoding(true));
            return path;
        }

        private static string LogRoot() =>
            Application.isEditor
                ? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs")
                : Application.persistentDataPath;
    }
}
