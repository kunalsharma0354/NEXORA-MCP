using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace NEXORA_MCP
{
    public class NexoraAiClient
    {
        private const string BaseUrl = "https://nexora-navy-omega.vercel.app";
        private readonly HttpClient _http;
        private readonly string _systemPrompt;

        public NexoraAiClient()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            _systemPrompt = LoadSystemPrompt();
        }

        private static string LoadSystemPrompt()
        {
            var dir = AppContext.BaseDirectory;
            var path = Path.Combine(dir, "sysprompot.txt");
            if (File.Exists(path))
                return File.ReadAllText(path);

            // Fallback: try relative to working directory
            path = Path.Combine(Directory.GetCurrentDirectory(), "sysprompot.txt");
            if (File.Exists(path))
                return File.ReadAllText(path);

            // Fallback: try in the NexoraMcp folder from the solution
            path = Path.Combine(Directory.GetCurrentDirectory(), "NexoraMcp", "sysprompot.txt");
            if (File.Exists(path))
                return File.ReadAllText(path);

            return "You are Nexora, a Prompt Intelligence Layer. Transform vague user requests into detailed execution-ready prompts. Output only the enhanced prompt.";
        }

        public async Task<string> EnhancePromptAsync(string userPrompt, string model, string? token = null, CancellationToken ct = default)
        {
            var requestBody = new
            {
                userPrompt = userPrompt,
                model = model,
                systemPrompt = _systemPrompt,
                thinking = true,
                temperature = 0.7,
                maxTokens = 16384,
                responseFormat = "json"
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/ai")
            {
                Content = JsonContent.Create(requestBody)
            };

            if (!string.IsNullOrWhiteSpace(token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var resp = await _http.SendAsync(req, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"NEXORA AI API returned {(int)resp.StatusCode}: {raw}");

            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                // Try common response shapes
                if (root.TryGetProperty("content", out var contentEl))
                    return contentEl.GetString() ?? raw;

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var first = choices[0];
                    if (first.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var mc))
                        return mc.GetString() ?? raw;
                }

                if (root.TryGetProperty("response", out var resp2))
                    return resp2.GetString() ?? raw;

                if (root.TryGetProperty("text", out var text))
                    return text.GetString() ?? raw;

                return raw;
            }
            catch
            {
                return raw;
            }
        }
    }
}
