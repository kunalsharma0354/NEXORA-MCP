using System.Text.Json;

namespace NEXORA_MCP
{
    public static class ActivityLogger
    {
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nexora");
        private static readonly string LogFile = Path.Combine(LogDir, "mcp_activity.json");
        private static readonly object _lock = new();
        private static readonly List<ActivityEntry> _entries = new();
        private static bool _loaded;

        public class ActivityEntry
        {
            public string Timestamp { get; set; } = "";
            public string Type { get; set; } = "";
            public string Tool { get; set; } = "";
            public string Input { get; set; } = "";
            public string Output { get; set; } = "";
            public string Model { get; set; } = "";
            public bool Success { get; set; }
            public string? Error { get; set; }
            public string? Status { get; set; }
        }

        static ActivityLogger()
        {
            Directory.CreateDirectory(LogDir);
            Load();
        }

        private static void Load()
        {
            if (_loaded) return;
            try
            {
                if (File.Exists(LogFile))
                {
                    var json = File.ReadAllText(LogFile);
                    var entries = JsonSerializer.Deserialize<List<ActivityEntry>>(json);
                    if (entries != null)
                        _entries.AddRange(entries);
                }
            }
            catch { }
            _loaded = true;
        }

        private static void Save()
        {
            try
            {
                // Keep last 100 entries
                while (_entries.Count > 100)
                    _entries.RemoveAt(0);

                var opts = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_entries, opts);
                File.WriteAllText(LogFile, json);
            }
            catch { }
        }

        public static string LogPending(string tool, string input, string model)
        {
            lock (_lock)
            {
                var id = Guid.NewGuid().ToString("N")[..8];
                _entries.Add(new ActivityEntry
                {
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    Type = "tool_call",
                    Tool = tool,
                    Input = Truncate(input, 500),
                    Output = "",
                    Model = model,
                    Success = false,
                    Status = "pending"
                });
                Save();
                return id;
            }
        }

        public static void CompletePending(string tool, string input, string output, string model, bool success, string? error = null)
        {
            lock (_lock)
            {
                var pending = _entries.LastOrDefault(e => e.Type == "tool_call" && e.Status == "pending" && e.Tool == tool);
                if (pending != null)
                {
                    pending.Output = Truncate(output, 2000);
                    pending.Model = model;
                    pending.Success = success;
                    pending.Error = error;
                    pending.Status = "done";
                }
                else
                {
                    _entries.Add(new ActivityEntry
                    {
                        Timestamp = DateTime.UtcNow.ToString("o"),
                        Type = "tool_call",
                        Tool = tool,
                        Input = Truncate(input, 500),
                        Output = Truncate(output, 2000),
                        Model = model,
                        Success = success,
                        Error = error,
                        Status = "done"
                    });
                }
                Save();
            }
        }

        public static void LogToolCall(string tool, string input, string output, string model, bool success, string? error = null)
        {
            lock (_lock)
            {
                _entries.Add(new ActivityEntry
                {
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    Type = "tool_call",
                    Tool = tool,
                    Input = Truncate(input, 500),
                    Output = Truncate(output, 2000),
                    Model = model,
                    Success = success,
                    Error = error,
                    Status = "done"
                });
                Save();
            }
        }

        public static void LogEvent(string type, string message)
        {
            lock (_lock)
            {
                _entries.Add(new ActivityEntry
                {
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    Type = type,
                    Tool = "",
                    Input = "",
                    Output = message,
                    Model = "",
                    Success = true
                });
                Save();
            }
        }

        public static List<ActivityEntry> GetEntries(int count = 50)
        {
            lock (_lock)
            {
                return _entries.TakeLast(count).Reverse().ToList();
            }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s[..max] + "...";
        }
    }
}
