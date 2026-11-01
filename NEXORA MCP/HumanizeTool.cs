using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace NEXORA_MCP
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MCP TOOL: nexora_humanize
    //
    //  AI agent text deta hai → tool use NEXORA ke apne /api/ai endpoint se
    //  humanize karke deta hai (ZeroGPT-validated pipeline):
    //    pass 1: casual-voice rewrite (recipe jo ZeroGPT pe 0% score deti hai)
    //    pass 2 (medium/heavy): local QA scan (HumanizeScan) + fix pass
    //    pass 2.5 (heavy only): fresh redraft pass
    //  Auth/session flow enhance_prompt jaisa hi: hot-reload + 401 self-heal.
    // ═══════════════════════════════════════════════════════════════════════════

    [McpServerToolType]
    public static class NexoraHumanizeTool
    {
        [McpServerTool(Name = "nexora_humanize")]
        [Description("HUMANIZE text: rewrites AI-sounding text so it reads like a real casual human wrote it, while preserving meaning. Uses a recipe validated against real AI detectors (ZeroGPT-tested). Use when the user asks to humanize text, make writing sound natural/human, or beat AI-detection scores. Feed the complete actual text, not instructions.")]
        public static async Task<string> RunAsync(
            IServiceProvider services,
            [Description("The AI-generated or stiff text to humanize. Provide the complete text itself.")] string text,
            [Description("Humanization depth: light = one natural rewrite pass; medium = rewrite + AI-tell removal (recommended); heavy = medium + rhythm balancing.")] string intensity = "medium",
            [Description("AI model to use. Default: nvidia/nemotron-3-ultra-550b-a55b")] string model = "nvidia/nemotron-3-ultra-550b-a55b")
        {
            var client = services.GetRequiredService<NexoraAiClient>();

            ActivityLogger.LogPending("nexora_humanize", text, model);

            try
            {
                // Hot-reload: picks up a fresh app login from session.json on
                // every call — OpenCode never needs restarting for token changes.
                TokenStore.EnsureFresh();
                if (!TokenStore.IsLoggedIn) TokenStore.LoadFromDisk();
                if (!TokenStore.IsLoggedIn)
                {
                    ActivityLogger.CompletePending("nexora_humanize", text, "", model, success: false, error: "Not logged in");
                    return "NEXORA MCP is not logged in. Open the NEXORA MCP app, sign in, then try again.";
                }

                var humanized = await client.HumanizeAsync(text, intensity, model, TokenStore.AccessToken);

                ActivityLogger.CompletePending("nexora_humanize", text, humanized, model, success: true);
                return humanized;
            }
            catch (Exception ex)
            {
                // Same 401 self-heal as enhance_prompt: drop the dead token from
                // memory so the next call reloads from disk after a fresh login.
                if (ex.Message.Contains("returned 401")) TokenStore.InvalidateMemory();

                var message = ex.Message.Contains("returned 401")
                    ? "NEXORA rejected your session token (401). Open the NEXORA MCP app and sign in again, then retry."
                    : ex.Message;

                ActivityLogger.CompletePending("nexora_humanize", text, "", model, success: false, error: message);
                return $"Error humanizing text: {message}";
            }
        }
    }
}
