using System.Diagnostics;
using System.Text.Json;

namespace NEXORA_MCP
{
    public static class OpenCodeSetup
    {
        private static readonly string[] PossiblePaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "opencode", "opencode.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "opencode", "opencode.jsonc"),
        };

        public static bool IsOpenCodeInstalled()
        {
            var paths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "opencode", "opencode.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "opencode", "opencode.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "opencode", "opencode.exe"),
            };

            foreach (var p in paths)
                if (File.Exists(p)) return true;

            try
            {
                var psi = new ProcessStartInfo("where", "opencode")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    var output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(5000);
                    return proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output);
                }
            }
            catch { }

            return false;
        }

        public static string? GetConfigPath()
        {
            foreach (var p in PossiblePaths)
                if (File.Exists(p)) return p;

            // Prefer .json over .jsonc
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "opencode", "opencode.json");
        }

        public static bool IsMcpRegistered()
        {
            var configPath = GetConfigPath();
            if (configPath == null || !File.Exists(configPath)) return false;

            try
            {
                var json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Check both "mcp" and "mcpServers" keys
                if (root.TryGetProperty("mcp", out var mcpSection) && mcpSection.ValueKind == JsonValueKind.Object)
                {
                    if (mcpSection.TryGetProperty("nexora", out _))
                        return true;
                }

                if (root.TryGetProperty("mcpServers", out var mcpServersSection) && mcpServersSection.ValueKind == JsonValueKind.Object)
                {
                    if (mcpServersSection.TryGetProperty("nexora", out _))
                        return true;
                }
            }
            catch { }

            return false;
        }

        public static bool RegisterMcpServer(string mcpExePath)
        {
            var configPath = GetConfigPath();
            if (configPath == null) return false;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

                string configJson;
                if (File.Exists(configPath))
                    configJson = File.ReadAllText(configPath);
                else
                    configJson = "{}";

                using var doc = JsonDocument.Parse(configJson);
                var root = doc.RootElement.Clone();

                using var ms = new MemoryStream();
                using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();

                    // Copy all existing properties EXCEPT "mcp" and "mcpServers"
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (prop.Name.Equals("mcp", StringComparison.OrdinalIgnoreCase) ||
                            prop.Name.Equals("mcpServers", StringComparison.OrdinalIgnoreCase))
                            continue;
                        prop.WriteTo(writer);
                    }

                    // Write "mcp" section (opencode's actual key)
                    writer.WritePropertyName("mcp");
                    writer.WriteStartObject();

                    // Copy existing entries from "mcp" if any
                    if (root.TryGetProperty("mcp", out var oldMcp) && oldMcp.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var server in oldMcp.EnumerateObject())
                            server.WriteTo(writer);
                    }

                    // Copy existing entries from "mcpServers" if any
                    if (root.TryGetProperty("mcpServers", out var oldServers) && oldServers.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var server in oldServers.EnumerateObject())
                            server.WriteTo(writer);
                    }

                    // Add nexora server
                    writer.WritePropertyName("nexora");
                    writer.WriteStartObject();
                    writer.WriteString("type", "local");
                    writer.WritePropertyName("command");
                    writer.WriteStartArray();
                    writer.WriteStringValue(mcpExePath);
                    writer.WriteStringValue("--mcp");
                    writer.WriteEndArray();
                    writer.WriteBoolean("enabled", true);
                    writer.WriteEndObject();

                    writer.WriteEndObject(); // mcp
                    writer.WriteEndObject(); // root
                }

                var newJson = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                File.WriteAllText(configPath, newJson);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void TryRestartOpenCode()
        {
            try
            {
                var processes = Process.GetProcessesByName("opencode");
                if (processes.Length == 0) return;

                foreach (var proc in processes)
                {
                    try
                    {
                        proc.CloseMainWindow();
                        proc.WaitForExit(3000);
                    }
                    catch { }
                }

                Thread.Sleep(1000);
                Process.Start(new ProcessStartInfo("opencode")
                {
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                });
            }
            catch { }
        }
    }
}
