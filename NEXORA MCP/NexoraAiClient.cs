using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NEXORA_MCP
{
    public class NexoraAiClient
    {
        private const string BaseUrl = "https://nexora-navy-omega.vercel.app";
        private readonly HttpClient _http;
        private readonly string _systemPrompt;
        private const string HumanizeModelDefault = "nvidia/nemotron-3-ultra-550b-a55b";

        /// <summary>
        /// Pass 1 humanize recipe - validated live against ZeroGPT
        /// (zerogpt.com): original AI text scored 100% fake, rewrites with this
        /// recipe scored 0% across two different test texts, stable on re-test.
        /// The recipe makes word choices less predictable and keeps a natural
        /// casual voice, which is what perplexity-based detectors key on -
        /// surface pattern removal alone does NOT work (it scored 66-70%).
        /// </summary>
        private const string HumanizeRecipePrompt =
            "You write like a real person talking, from their point of view, casual but clear. Rules:\n" +
            "1. Keep roughly the same length as the input (never more than 1.2x, never fewer words).\n" +
            "2. Use contractions (didn't, can't, you'll, it's).\n" +
            "3. Pick slightly unusual but natural word choices; never the most obvious synonym. " +
            "Words like 'particularly', 'straightforward', 'advancement', 'inquisitive', 'momentum', 'diverge' are BANNED - use normal people words.\n" +
            "4. Sentence lengths must jump around: some 2-5 words, some 15-25.\n" +
            "5. No em dashes, no semicolons, no neat three-item lists, no AI words " +
            "(journey, glacial, needle, deadline, foster, leverage, moreover, crucial, " +
            "resilience, delve, seamless, pivotal, tapestry, game-changer, cutting-edge, " +
            "unlock, navigate, curveball, empowering, impactful, transformative, " +
            "one-size-fits-all, remarkable, consistent).\n" +
            "6. No 'the key is / the trick is' openers, no 'no universal/master/fixed timeline' phrases.\n" +
            "7. Do not add new facts, opinions or feelings that are not in the input.\n" +
            "8. Do not start two consecutive sentences with the same word.\n" +
            "9. Output ONLY the rewritten text.";

        /// <summary>Fix pass used when the local QA scan (HumanizeScan) still
        /// finds residual AI tells after a rewrite pass.</summary>
        private const string HumanizeFixPrompt =
            "You fix AI-detection issues in text by rewording the flagged spots with plain everyday phrasing. " +
            "Keep everything else about the text exactly as it is: same meaning, same length, same casual voice. " +
            "No em dashes, no semicolons, no invented facts, no stiff formal words. " +
            "Output ONLY the fixed text.";

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

            return await SendAiAsync(requestBody, "ai", token, ct);
        }

        /// <summary>
        /// Multi-pass AI text humanization through the NEXORA AI endpoint,
        /// with a local QA scan gate (HumanizeScan).
        ///   light  = rewrite + QA fix (1 try), redraft escalate if flagged
        ///   medium = rewrite + redraft + QA fix (2 tries)
        ///   heavy  = rewrite + 2 redrafts + QA fix (2 tries) + last fix pass
        /// The redraft pass is what reliably lands ZeroGPT-green results in
        /// live tests - without it scores fluctuate badly.
        /// </summary>
        public async Task<string> HumanizeAsync(string text, string intensity, string model, string? token = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new Exception("Humanize input text is empty.");

            model = string.IsNullOrWhiteSpace(model) ? HumanizeModelDefault : model;
            intensity = (intensity ?? "medium").Trim().ToLowerInvariant();

            // Pass 1 - casual-voice rewrite (ZeroGPT-validated recipe).
            var originalWords = System.Text.RegularExpressions.Regex.Matches(text, @"\S+").Count;
            var current = await SendAiAsync(
                new
                {
                    userPrompt = "Rewrite this text. Keep the meaning. Output ONLY rewritten text:\n\n" + text,
                    model = model,
                    systemPrompt = HumanizeRecipePrompt,
                    thinking = false,
                    temperature = 1.05,
                    maxTokens = 16000,
                    responseFormat = "text"
                },
                "humanize", token, ct);

            if (intensity != "light")
            {
                // Fresh redraft with new word choices (reliably detaches the
                // text's statistical profile from the input).
                current = await RedraftAsync(current, model, 1.15, token, ct);

                if (intensity == "heavy")
                {
                    // Second redraft pushes phrasing even further from source.
                    current = await RedraftAsync(current, model, 1.2, token, ct);
                }
            }

            // QA loop: scan locally, reword via fix pass while tells remain.
            var maxFix = intensity == "light" ? 1 : 2;
            var issues = HumanizeScan.Check(current, originalWords);
            for (var attempt = 0; attempt < maxFix && issues.Count > 0; attempt++)
            {
                current = await FixAsync(current, issues, model, token, ct);
                issues = HumanizeScan.Check(current, originalWords);
            }

            // Light-mode escalation - QA found residual tells: redraft, fix.
            if (issues.Count > 0 && intensity == "light")
            {
                current = await RedraftAsync(current, model, 1.15, token, ct);
                issues = HumanizeScan.Check(current, originalWords);
                if (issues.Count > 0)
                    current = await FixAsync(current, issues, model, token, ct);
            }

            // Heavy: one last surgical fix attempt if scan still flags.
            if (issues.Count > 0 && intensity == "heavy")
            {
                current = await FixAsync(current, issues, model, token, ct);
            }

            return current;
        }

        private async Task<string> RedraftAsync(string text, string model, double temperature, string? token, CancellationToken ct)
        {
            return await SendAiAsync(
                new
                {
                    userPrompt = "Redraft this text: same rules, same meaning, same length, but change most word choices so no phrase stays obvious, stiff or AI-flavored. Output ONLY the redraft:\n\n" + text,
                    model = model,
                    systemPrompt = HumanizeRecipePrompt,
                    thinking = false,
                    temperature = temperature,
                    maxTokens = 16000,
                    responseFormat = "text"
                },
                "humanize", token, ct);
        }

        private async Task<string> FixAsync(string text, List<string> issues, string model, string? token, CancellationToken ct)
        {
            var notes = string.Join("\n- ", issues);
            return await SendAiAsync(
                new
                {
                    userPrompt = "The text has these exact problems:\n- " + notes + "\n\nFix them by REWORDING those spots with plain everyday phrasing. Keep the same meaning, keep every other sentence as it is, keep the casual voice. Output ONLY the fixed text:\n\n" + text,
                    model = model,
                    systemPrompt = HumanizeFixPrompt,
                    thinking = false,
                    temperature = 0.8,
                    maxTokens = 16000,
                    responseFormat = "text"
                },
                "humanize", token, ct);
        }

        private async Task<string> SendAiAsync(object requestBody, string networkSource, string? token, CancellationToken ct)
        {
            var payload = JsonSerializer.Serialize(requestBody);
            var aiUrl = $"{BaseUrl}/api/ai";

            var net = NetworkLog.Begin(networkSource, "POST", aiUrl, payload.Length);

            using var req = new HttpRequestMessage(HttpMethod.Post, aiUrl)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (!string.IsNullOrWhiteSpace(token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage resp;
            string raw;
            try
            {
                resp = await _http.SendAsync(req, ct);
                raw = await resp.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex)
            {
                NetworkLog.Fail(net, ex.Message);
                throw;
            }

            NetworkLog.Complete(net, (int)resp.StatusCode, raw.Length);

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"NEXORA AI API returned {(int)resp.StatusCode}: {raw}");

            return ParseAiContent(raw);
        }

        /// <summary>Extracts the model text from the /api/ai envelope. Tries the
        /// common shapes: content, choices[0].message.content, response, text,
        /// falling back to the raw body.</summary>
        public static string ParseAiContent(string raw)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                if (root.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.String)
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
