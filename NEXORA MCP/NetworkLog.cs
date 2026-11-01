using System.Diagnostics;
using System.Text.Json;

namespace NEXORA_MCP
{
    /// <summary>
    /// DevTools-style network log for NEXORA.
    ///
    /// Every outbound HTTP request made by NEXORA components (auth login,
    /// AI enhance, ...) is captured in a bounded ring buffer and persisted to
    /// %LocalAppData%\Nexora\network_log.json so that both the desktop app and
    /// the MCP tools (nexora_network_log) can inspect the real traffic —
    /// including across process restarts.
    /// </summary>
    public static class NetworkLog
    {
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nexora");
        private static readonly string LogFile = Path.Combine(LogDir, "network_log.json");

        /// <summary>Maximum entries kept in memory and on disk.</summary>
        public const int MaxEntries = 200;

        private static readonly object _lock = new();
        private static readonly List<Entry> _entries = new();

        public sealed class Entry
        {
            public string Timestamp { get; set; } = "";
            /// <summary>Which component made the request: "auth", "ai", …</summary>
            public string Source { get; set; } = "";
            public string Method { get; set; } = "";
            public string Url { get; set; } = "";
            /// <summary>HTTP status code, or -1 when no response ever arrived
            /// (timeout, DNS failure, connection refused…).</summary>
            public int StatusCode { get; set; } = -1;
            public double DurationMs { get; set; }
            public int RequestBytes { get; set; }
            public int ResponseBytes { get; set; }
            public bool Success { get; set; }
            public string? Error { get; set; }

            internal Stopwatch? _sw;
        }

        static NetworkLog()
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                if (File.Exists(LogFile))
                {
                    var json = File.ReadAllText(LogFile);
                    var loaded = JsonSerializer.Deserialize<List<Entry>>(json);
                    if (loaded != null)
                        _entries.AddRange(loaded);
                }
            }
            catch
            {
                // A corrupt log must never break the app — start clean.
                _entries.Clear();
            }
        }

        /// <summary>Creates a new entry when a request is about to be sent.
        /// Call <see cref="Complete"/> or <see cref="Fail"/> on it afterwards.</summary>
        public static Entry Begin(string source, string method, string url, int requestBytes = 0)
        {
            return new Entry
            {
                Timestamp = DateTime.UtcNow.ToString("o"),
                Source = source,
                Method = method,
                Url = url,
                RequestBytes = requestBytes,
                _sw = Stopwatch.StartNew()
            };
        }

        /// <summary>Records a finished request that produced an HTTP response.</summary>
        public static void Complete(Entry e, int statusCode, int responseBytes)
        {
            e.StatusCode = statusCode;
            e.ResponseBytes = responseBytes;
            e.Success = statusCode >= 200 && statusCode < 400;
            Store(e);
        }

        /// <summary>Records a request that failed without a usable HTTP response
        /// (exception, timeout...). Pass a status code if one was received.</summary>
        public static void Fail(Entry e, string error, int statusCode = -1)
        {
            e.StatusCode = statusCode;
            e.Success = false;
            e.Error = Truncate(error, 400);
            Store(e);
        }

        private static void Store(Entry e)
        {
            if (e._sw != null)
            {
                e._sw.Stop();
                e.DurationMs = Math.Round(e._sw.Elapsed.TotalMilliseconds, 1);
            }

            lock (_lock)
            {
                // The Begin entry is added here (not in Begin) so that an entry
                // never sits unfinished in the buffer/persisted file.
                _entries.Add(e);
                while (_entries.Count > MaxEntries)
                    _entries.RemoveAt(0);

                Save();
            }
        }

        private static void Save()
        {
            try
            {
                var opts = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(LogFile, JsonSerializer.Serialize(_entries, opts));
            }
            catch
            {
                // Disk hiccup — logging must never break real traffic.
            }
        }

        /// <summary>Thread-safe copy of the newest matching entries (newest first).</summary>
        public static List<Entry> Snapshot(int limit, string? filter = null, bool failuresOnly = false)
        {
            lock (_lock)
            {
                IEnumerable<Entry> q = _entries.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(filter))
                {
                    var f = filter.Trim();
                    q = q.Where(e =>
                        e.Url.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                        e.Source.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                        (e.Error?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        e.StatusCode.ToString() == f);
                }

                if (failuresOnly)
                    q = q.Where(e => !e.Success);

                return q.TakeLast(Math.Clamp(limit, 1, MaxEntries)).Reverse().ToList();
            }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s[..max] + "...";
        }
    }
}
