using System;
using System.IO;
using UnityEngine;

namespace Interrogation.Llm
{
    public enum ApiKeySource { None, ProcessEnv, UserEnv, MachineEnv, DotEnvFile }

    /// <summary>
    /// Az API-kulcs feloldása, hogy a kulcs soha ne kerüljön a kódba.
    /// Sorrend: process env → Windows User env → Machine env → projekt-helyi .env fájl.
    /// </summary>
    public static class ApiKeyProvider
    {
        public static string Resolve(string varName, out ApiKeySource source)
        {
            var v = Environment.GetEnvironmentVariable(varName);
            if (!string.IsNullOrWhiteSpace(v)) { source = ApiKeySource.ProcessEnv; return v.Trim(); }

            // A registry-t élőben olvassuk, így a futó Editor újraindítás nélkül is látja az új kulcsot.
            if (IsWindows())
            {
                v = SafeGet(varName, EnvironmentVariableTarget.User);
                if (!string.IsNullOrWhiteSpace(v)) { source = ApiKeySource.UserEnv; return v.Trim(); }

                v = SafeGet(varName, EnvironmentVariableTarget.Machine);
                if (!string.IsNullOrWhiteSpace(v)) { source = ApiKeySource.MachineEnv; return v.Trim(); }
            }

            v = ReadFromDotEnv(varName);
            if (!string.IsNullOrWhiteSpace(v)) { source = ApiKeySource.DotEnvFile; return v.Trim(); }

            source = ApiKeySource.None;
            return null;
        }

        public static string DotEnvPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".env"));

        private static bool IsWindows() =>
            Application.platform == RuntimePlatform.WindowsEditor ||
            Application.platform == RuntimePlatform.WindowsPlayer;

        private static string SafeGet(string name, EnvironmentVariableTarget target)
        {
            try { return Environment.GetEnvironmentVariable(name, target); }
            catch { return null; } // nem-Windows platformon dobhat
        }

        private static string ReadFromDotEnv(string varName)
        {
            try
            {
                var path = DotEnvPath;
                if (!File.Exists(path)) return null;

                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;

                    var eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    var key = line.Substring(0, eq).Trim();
                    if (!string.Equals(key, varName, StringComparison.Ordinal)) continue;

                    var val = line.Substring(eq + 1).Trim();
                    val = StripQuotes(val);
                    return val;
                }
            }
            catch { /* a fallback hiánya nem kritikus */ }
            return null;
        }

        private static string StripQuotes(string s)
        {
            if (s.Length >= 2 &&
                ((s[0] == '"' && s[s.Length - 1] == '"') ||
                 (s[0] == '\'' && s[s.Length - 1] == '\'')))
                return s.Substring(1, s.Length - 2);
            return s;
        }
    }
}
