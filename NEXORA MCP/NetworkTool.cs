using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace NEXORA_MCP
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MCP TOOL: nexora_network_log
    //
    //  Read-only, DevTools-style network log. Surfaces the outbound HTTP
    //  traffic NEXORA actually produced (login attempts, AI enhance calls...)
    //  with real status codes, durations, payload sizes and error text.
    //  KunalDebug: perfect for chasing 401/429/timeout issues from OpenCode.
    // ═══════════════════════════════════════════════════════════════════════════

    [McpServerToolType]
    public static class NexoraNetworkLogTool
    {
        [McpServerTool(Name = "nexora_network_log")]
        [Description("DevTools-style network log for NEXORA: lists recent outbound HTTP requests made by the app (auth login, AI enhance, ...). Each entry shows timestamp, source, method, URL, HTTP status code (-1 = no response, e.g. timeout), duration in ms, request/response byte sizes and error text. Use it to debug login failures (401/403/429), network errors and API latency. Read-only.")]
        public static string Get(
            [Description("Maximum number of recent requests to return (1-200). Default 25.")] int limit = 25,
            [Description("Optional substring filter matched against URL, source or error text, e.g. \"auth-login\" or \"timeout\".")] string? filter = null,
            [Description("When true, returns only failed requests (HTTP >= 400 or network-level failures).")] bool failures_only = false)
        {
            try
            {
                var entries = NetworkLog.Snapshot(limit, filter, failures_only);

                var payload = new
                {
                    ok = true,
                    count = entries.Count,
                    failuresOnly = failures_only,
                    filter,
                    requests = entries.Select(e => new Dictionary<string, object?>
                    {
                        ["timestamp"] = e.Timestamp,
                        ["source"] = e.Source,
                        ["method"] = e.Method,
                        ["url"] = e.Url,
                        ["status"] = e.StatusCode,
                        ["durationMs"] = e.DurationMs,
                        ["requestBytes"] = e.RequestBytes,
                        ["responseBytes"] = e.ResponseBytes,
                        ["success"] = e.Success,
                        ["error"] = e.Error
                    })
                };

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                // A first-run log is legitimately empty; guide the caller instead
                // of returning a bare {"count":0} that invites wrong conclusions.
                if (entries.Count == 0)
                {
                    return JsonSerializer.Serialize(new
                    {
                        ok = true,
                        count = 0,
                        hint = "No HTTP traffic captured yet. Entries appear after NEXORA makes an auth or AI request; the log also persists on disk at %LocalAppData%\\Nexora\\network_log.json."
                    });
                }

                return json;
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { ok = false, error = ex.Message });
            }
        }
    }
}
