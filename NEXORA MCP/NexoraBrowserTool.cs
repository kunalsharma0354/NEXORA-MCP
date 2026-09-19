using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace NEXORA_MCP
{
    // ════════════════════════════════════════════════════════════════════════
    //  MCP TOOLS: nexora_browser_*
    //  Drive the shared visible browser (BrowserSession) so the AI agent can
    //  view real websites, click/type, use proxy, read page content, and run
    //  arbitrary JS. Every call is logged to the dashboard via ActivityLogger.
    // ════════════════════════════════════════════════════════════════════════

    [McpServerToolType]
    public static class NexoraBrowserTool
    {
        private static BrowserSession GetSession(IServiceProvider sp) => sp.GetRequiredService<BrowserSession>();

        private static string Json(object? obj) => JsonSerializer.Serialize(obj);

        // Parse "http://user:pass@host:port" / "socks5://host:port" into proxy fields
        private static (string Server, string? User, string? Pass) ParseProxy(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return ("", null, null);
            var v = value.Trim();
            string server;
            string? user = null, pass = null;
            var schemeIdx = v.IndexOf("://", StringComparison.Ordinal);
            var atIdx = v.LastIndexOf('@');
            if (atIdx >= 0 && (schemeIdx < 0 || atIdx > schemeIdx))
            {
                var creds = v[(schemeIdx + 3)..atIdx];
                server = v[..schemeIdx] + "://" + v[(atIdx + 1)..];
                var colon = creds.IndexOf(':');
                if (colon >= 0)
                {
                    user = Uri.UnescapeDataString(creds[..colon]);
                    pass = Uri.UnescapeDataString(creds[(colon + 1)..]);
                }
                else user = Uri.UnescapeDataString(creds);
            }
            else server = v;
            return (server, user, pass);
        }

        [McpServerTool(Name = "nexora_browser_status")]
        [Description("Check the shared embedded browser status: running state, active page URL/title, tab count, proxy, profile path. Use this before other browser tools to see what's open.")]
        public static async Task<string> StatusAsync(
            IServiceProvider services,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            try
            {
                var status = await Task.Run(() => s.Status(), ct).ConfigureAwait(false);
                return Json(status);
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [McpServerTool(Name = "nexora_browser_launch")]
        [Description("Ensure the embedded browser (inside the NEXORA MCP app) is available. Idempotent. Optionally set a proxy URL at launch, e.g. http://host:port or socks5://host:port. The browser lives inside the app window the user has open — same browser the AI drives. Login state (Gmail etc.) persists. If it fails, ask the user to open the NEXORA MCP app.")]
        public static async Task<string> LaunchAsync(
            IServiceProvider services,
            [Description("Proxy server URL, e.g. http://user:pass@host:port or socks5://host:port. Empty string disables.")] string? proxy = null,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            try
            {
                var (server, user, pass) = ParseProxy(proxy);
                var result = await s.EnsureLaunchedAsync(string.IsNullOrWhiteSpace(server) ? null : server, user, pass, ct).ConfigureAwait(false);
                return Json(result);
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [McpServerTool(Name = "nexora_browser_navigate")]
        [Description("Navigate the active browser tab. Accepts a URL ('https://...', 'example.com') or a search query — plain text is searched on Google. Returns the final URL, page title, and load time. Use waitUntil=networkidle for JS-heavy sites, load for speed.")]
        public static async Task<string> NavigateAsync(
            IServiceProvider services,
            [Description("The URL to open, e.g. https://example.com")] string url,
            [Description("Wait strategy: load, domcontentloaded, commit, or networkidle.")] string waitUntil = "load",
            [Description("Navigation timeout in milliseconds.")] int timeoutMs = 60000,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            ActivityLogger.LogPending("nexora_browser_navigate", url, "");
            try
            {
                var result = await s.NavigateAsync(url, waitUntil, timeoutMs, ct).ConfigureAwait(false);
                ActivityLogger.CompletePending("nexora_browser_navigate", url, Json(result), "", success: result.Success, error: result.Success ? null : result.Error);
                return Json(result);
            }
            catch (Exception ex)
            {
                ActivityLogger.CompletePending("nexora_browser_navigate", url, "", "", success: false, error: ex.Message);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [McpServerTool(Name = "nexora_browser_get_content")]
        [Description("Extract readable text (or HTML) from the current page. Use for website review/reading: it returns the article/main content or a CSS selector's content. Great for summarizing pages, extracting data, verifying text.")]
        public static async Task<string> GetContentAsync(
            IServiceProvider services,
            [Description("Output format: 'text' or 'html'.")] string format = "text",
            [Description("Optional CSS selector to extract only that element. Empty extracts the page main/article/body.")] string? selector = null,
            [Description("Maximum characters to return (default 50000).")] int maxChars = 50000,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            ActivityLogger.LogPending("nexora_browser_get_content", $"format={format}, selector={selector ?? "(body)"}", "");
            try
            {
                var result = await s.GetContentAsync(format, string.IsNullOrWhiteSpace(selector) ? null : selector, maxChars, ct).ConfigureAwait(false);
                ActivityLogger.CompletePending("nexora_browser_get_content", $"{format}:{selector ?? "body"}", $"{result.Content.Length} chars", "", success: result.Success, error: result.Success ? null : result.Error);
                return Json(result);
            }
            catch (Exception ex)
            {
                ActivityLogger.CompletePending("nexora_browser_get_content", format, "", "", success: false, error: ex.Message);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [McpServerTool(Name = "nexora_browser_screenshot")]
        [Description("Take a screenshot of the current page. Returns a base64 PNG data URL AND the saved file path on disk. Use when you need to visually inspect layout, forms, or content that text extraction can't capture.")]
        public static async Task<string> ScreenshotAsync(
            IServiceProvider services,
            [Description("Capture the entire scrollable page instead of just the viewport.")] bool fullPage = false,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            ActivityLogger.LogPending("nexora_browser_screenshot", fullPage ? "fullPage" : "viewport", "");
            try
            {
                var result = await s.TakeScreenshotAsync(fullPage, ct).ConfigureAwait(false);
                ActivityLogger.CompletePending("nexora_browser_screenshot", fullPage ? "fullPage" : "viewport", result.Success ? $"{result.ByteSize} bytes -> {result.FilePath}" : "", "", success: result.Success, error: result.Success ? null : "Screenshot failed");
                return Json(result);
            }
            catch (Exception ex)
            {
                ActivityLogger.CompletePending("nexora_browser_screenshot", fullPage ? "fullPage" : "viewport", "", "", success: false, error: ex.Message);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [McpServerTool(Name = "nexora_browser_click")]
        [Description("Click an element (button, link, checkbox) in the active page using a CSS selector. Use for interacting with the browser: clicking 'Compose' in Gmail, submitting forms, opening links.")]
        public static async Task<string> ClickAsync(
            IServiceProvider services,
            [Description("CSS selector of the element to click, e.g. 'div[role=button]:has-text(\"Compose\")'.")] string selector,
            [Description("Mouse button: left, right, or middle.")] string button = "left",
            [Description("Timeout in milliseconds.")] int timeoutMs = 30000,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            ActivityLogger.LogPending("nexora_browser_click", selector, "");
            try
            {
                var result = await s.ClickAsync(selector, button, timeoutMs, ct).ConfigureAwait(false);
                ActivityLogger.CompletePending("nexora_browser_click", selector, result.Success ? "clicked" : "", "", success: result.Success, error: result.Success ? null : result.Result);
                return Json(result);
            }
            catch (Exception ex)
            {
                ActivityLogger.CompletePending("nexora_browser_click", selector, "", "", success: false, error: ex.Message);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [McpServerTool(Name = "nexora_browser_fill")]
        [Description("Fill an input/textarea with a value (clears existing content first). Use for text fields, search boxes, login forms, Gmail recipients/subject.")]
        public static async Task<string> FillAsync(
            IServiceProvider services,
            [Description("CSS selector of the input element.")] string selector,
            [Description("The value to fill in.")] string value,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            ActivityLogger.LogPending("nexora_browser_fill", $"{selector} = {Truncate(value, 80)}", "");
            try
            {
                var result = await s.FillAsync(selector, value, false, 30000, ct).ConfigureAwait(false);
                ActivityLogger.CompletePending("nexora_browser_fill", selector, result.Success ? "filled" : "", "", success: result.Success, error: result.Success ? null : result.Result);
                return Json(result);
            }
            catch (Exception ex)
            {
                ActivityLogger.CompletePending("nexora_browser_fill", selector, "", "", success: false, error: ex.Message);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [McpServerTool(Name = "nexora_browser_type")]
        [Description("Type text into a focused element (character by character, realistic). Use for contenteditable bodies like Gmail compose body, or after clicking an element.")]
        public static async Task<string> TypeAsync(
            IServiceProvider services,
            [Description("CSS selector of the element to type into.")] string selector,
            [Description("The text to type.")] string text,
            [Description("Delay between keystrokes in ms (default 20).")] int delayMs = 20,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            ActivityLogger.LogPending("nexora_browser_type", $"{selector} = {Truncate(text, 80)}", "");
            try
            {
                var result = await s.TypeAsync(selector, text, delayMs, 30000, ct).ConfigureAwait(false);
                ActivityLogger.CompletePending("nexora_browser_type", selector, result.Success ? "typed" : "", "", success: result.Success, error: result.Success ? null : result.Result);
                return Json(result);
            }
            catch (Exception ex)
            {
                ActivityLogger.CompletePending("nexora_browser_type", selector, "", "", success: false, error: ex.Message);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [McpServerTool(Name = "nexora_browser_press_key")]
        [Description("Press a keyboard key or shortcut in the active page, optionally after focusing a selector. Keys: Enter, Tab, Escape, ArrowDown, Control+A, Control+Enter, etc.")]
        public static async Task<string> PressKeyAsync(
            IServiceProvider services,
            [Description("The key or shortcut: Enter, Tab, Control+S, etc.")] string key,
            [Description("Optional CSS selector to focus first.")] string? selector = null,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            ActivityLogger.LogPending("nexora_browser_press_key", $"{key} @ {selector ?? "(page)"}", "");
            try
            {
                var result = await s.PressKeyAsync(key, string.IsNullOrWhiteSpace(selector) ? null : selector, 30000, ct).ConfigureAwait(false);
                ActivityLogger.CompletePending("nexora_browser_press_key", key, result.Success ? "pressed" : "", "", success: result.Success, error: result.Success ? null : result.Result);
                return Json(result);
            }
            catch (Exception ex)
            {
                ActivityLogger.CompletePending("nexora_browser_press_key", key, "", "", success: false, error: ex.Message);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [McpServerTool(Name = "nexora_browser_wait_for")]
        [Description("Wait for the page to reach a load state (networkidle/load/domcontentloaded) or a selector to appear. Use after clicking buttons that trigger AJAX (e.g. after Gmail Send).")]
        public static async Task<string> WaitForAsync(
            IServiceProvider services,
            [Description("Load state: networkidle, load, or domcontentloaded.")] string state = "networkidle",
            [Description("Timeout in milliseconds.")] int timeoutMs = 30000,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            ActivityLogger.LogPending("nexora_browser_wait_for", state, "");
            try
            {
                var result = await s.WaitForAsync(state, timeoutMs, ct).ConfigureAwait(false);
                ActivityLogger.CompletePending("nexora_browser_wait_for", state, result.Success ? "ready" : "", "", success: result.Success, error: result.Success ? null : result.Error);
                return Json(result);
            }
            catch (Exception ex)
            {
                ActivityLogger.CompletePending("nexora_browser_wait_for", state, "", "", success: false, error: ex.Message);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [McpServerTool(Name = "nexora_browser_evaluate")]
        [Description("Run arbitrary JavaScript in the current page and return the result (JSON serialized). Use for scraping, custom DOM access, shadow DOM, reading page state the other tools can't.")]
        public static async Task<string> EvaluateAsync(
            IServiceProvider services,
            [Description("JavaScript expression to run in the page, e.g. '() => document.title' or '() => document.querySelectorAll(\"a\").length'.")] string script,
            [Description("Optional JSON-stringified argument passed to the script as a function argument.")] string? argJson = null,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            ActivityLogger.LogPending("nexora_browser_evaluate", Truncate(script, 120), "");
            try
            {
                var result = await s.EvaluateAsync(script, argJson, ct).ConfigureAwait(false);
                ActivityLogger.CompletePending("nexora_browser_evaluate", Truncate(script, 120), Truncate(result.Result, 120), "", success: result.Success, error: result.Success ? null : result.Result);
                return Json(result);
            }
            catch (Exception ex)
            {
                ActivityLogger.CompletePending("nexora_browser_evaluate", Truncate(script, 120), "", "", success: false, error: ex.Message);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [McpServerTool(Name = "nexora_browser_get_tabs")]
        [Description("List all open tabs in the shared browser (index, URL, title, active). Open new tabs with nexora_browser_open_tab; popups and target=_blank links also become tabs.")]
        public static async Task<string> GetTabsAsync(
            IServiceProvider services,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            try
            {
                var tabs = await s.GetTabsAsync(ct).ConfigureAwait(false);
                return Json(tabs);
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [McpServerTool(Name = "nexora_browser_switch_tab")]
        [Description("Switch the active tab by index (see nexora_browser_get_tabs). Subsequent browser actions run against the selected tab.")]
        public static async Task<string> SwitchTabAsync(
            IServiceProvider services,
            [Description("Zero-based tab index to activate.")] int index,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            try
            {
                var result = await s.SwitchTabAsync(index, ct).ConfigureAwait(false);
                return Json(result);
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [McpServerTool(Name = "nexora_browser_close_tab")]
        [Description("Close a tab by index (or the active tab if no index given).")]
        public static async Task<string> CloseTabAsync(
            IServiceProvider services,
            [Description("Optional zero-based tab index to close. Omit to close the active tab.")] int? index = null,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            try
            {
                var result = await s.CloseTabAsync(index, ct).ConfigureAwait(false);
                return Json(result);
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [McpServerTool(Name = "nexora_browser_set_proxy")]
        [Description("Set or change the proxy for the browser session. Restarts the browser context but KEEPS the profile (logins persist). Pass empty server to disable proxy (direct connection). Format: http://host:port, socks5://host:port, or with credentials http://user:pass@host:port.")]
        public static async Task<string> SetProxyAsync(
            IServiceProvider services,
            [Description("Proxy server URL, e.g. http://127.0.0.1:8080. Empty/null to disable.")] string? server = null,
            [Description("Optional proxy username.")] string? username = null,
            [Description("Optional proxy password.")] string? password = null,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            ActivityLogger.LogPending("nexora_browser_set_proxy", server ?? "none", "");
            try
            {
                var result = await s.SetProxyAsync(string.IsNullOrWhiteSpace(server) ? null : server, username, password, ct).ConfigureAwait(false);
                ActivityLogger.CompletePending("nexora_browser_set_proxy", server ?? "none", result.Success ? $"applied (restarted={result.Restarted})" : "", "", success: result.Success);
                return Json(result);
            }
            catch (Exception ex)
            {
                ActivityLogger.CompletePending("nexora_browser_set_proxy", server ?? "none", "", "", success: false, error: ex.Message);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [McpServerTool(Name = "nexora_browser_close")]
        [Description("Close/reset the embedded browser view (the app keeps running; the browser panel is preserved for next use).")]
        public static async Task<string> CloseAsync(
            IServiceProvider services,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            try
            {
                var result = await s.CloseAsync(ct).ConfigureAwait(false);
                return Json(result);
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s[..max] + "...";
        }

        // ═══════════════ ULTRA PACK: virtual cursor, scroll/drag, history, tabs, testing ═══════════════

        [McpServerTool(Name = "nexora_browser_open_tab")]
        [Description("Open a new tab (optionally with a URL or search query — plain text searches Google) and switch to it. Returns the new tab info. Popups and target=_blank links also become real tabs automatically.")]
        public static async Task<string> OpenTabAsync(
            IServiceProvider services,
            [Description("URL or search query to open. Empty opens the Google start page.")] string? url = null,
            [Description("Wait strategy: load, domcontentloaded, commit, or networkidle.")] string waitUntil = "load",
            [Description("Navigation timeout in milliseconds.")] int timeoutMs = 60000,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            ActivityLogger.LogPending("nexora_browser_open_tab", url ?? "(blank)", "");
            try
            {
                var result = await s.OpenTabAsync(string.IsNullOrWhiteSpace(url) ? null : url, waitUntil, timeoutMs, ct).ConfigureAwait(false);
                ActivityLogger.CompletePending("nexora_browser_open_tab", url ?? "(blank)", result.Url, "", success: true, error: null);
                return Json(result);
            }
            catch (Exception ex)
            {
                ActivityLogger.CompletePending("nexora_browser_open_tab", url ?? "(blank)", "", "", success: false, error: ex.Message);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [McpServerTool(Name = "nexora_browser_click_at")]
        [Description("VIRTUAL CURSOR CLICK: click at exact screenshot pixels. Loop: 1) nexora_browser_screenshot, 2) read the target's x/y from THAT image, 3) call this with those pixels. Use for anything without a selector: canvas, maps, sliders, video controls, custom widgets. Supports left/right/middle buttons and double/triple click. If the page scrolled or navigated since the screenshot, re-screenshot first.")]
        public static async Task<string> ClickAtAsync(
            IServiceProvider services,
            [Description("X pixel in the latest screenshot image.")] int x,
            [Description("Y pixel in the latest screenshot image.")] int y,
            [Description("Mouse button: left, right, or middle.")] string button = "left",
            [Description("Click count: 1 = click, 2 = double-click, 3 = triple-click.")] int clicks = 1,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            ActivityLogger.LogPending("nexora_browser_click_at", $"({x},{y}) {button}x{clicks}", "");
            try
            {
                var result = await s.ClickAtAsync(x, y, button, clicks, ct).ConfigureAwait(false);
                ActivityLogger.CompletePending("nexora_browser_click_at", $"({x},{y})", result.Result, "", success: result.Success, error: result.Success ? null : result.Result);
                return Json(result);
            }
            catch (Exception ex)
            {
                ActivityLogger.CompletePending("nexora_browser_click_at", $"({x},{y})", "", "", success: false, error: ex.Message);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [McpServerTool(Name = "nexora_browser_hover_at")]
        [Description("VIRTUAL CURSOR HOVER: move the mouse to screenshot pixels (from the latest nexora_browser_screenshot) without clicking. Use to reveal tooltips, dropdown menus, hover states before clicking. Re-screenshot first if the page scrolled or changed.")]
        public static async Task<string> HoverAtAsync(
            IServiceProvider services,
            [Description("X pixel in the latest screenshot image.")] int x,
            [Description("Y pixel in the latest screenshot image.")] int y,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            try
            {
                var result = await s.HoverAtAsync(x, y, ct).ConfigureAwait(false);
                return Json(result);
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [McpServerTool(Name = "nexora_browser_scroll")]
        [Description("Scroll the page (or an element) up/down/left/right by pixels (default 600). Uses the viewport center unless a CSS selector of a scrollable element (chat box, dropdown list) is given. Returns the new scroll position.")]
        public static async Task<string> ScrollAsync(
            IServiceProvider services,
            [Description("Direction: up, down, left, or right.")] string direction = "down",
            [Description("Pixels per scroll tick (10-5000).")] int pixels = 600,
            [Description("Optional X pixel (screenshot coords) to scroll at. -1 = viewport center.")] int x = -1,
            [Description("Optional Y pixel (screenshot coords) to scroll at. -1 = viewport center.")] int y = -1,
            [Description("Optional CSS selector of a scrollable element (chat box, dropdown list).")] string? selector = null,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            try
            {
                var result = await s.ScrollAsync(direction, pixels, x, y, string.IsNullOrWhiteSpace(selector) ? null : selector, ct).ConfigureAwait(false);
                return Json(result);
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [McpServerTool(Name = "nexora_browser_drag")]
        [Description("Drag from one screenshot point to another (screenshot pixels from the latest nexora_browser_screenshot). Use for sliders, range inputs, map panning, drag-and-drop, drawing on canvas.")]
        public static async Task<string> DragAsync(
            IServiceProvider services,
            [Description("Start X pixel in the latest screenshot.")] int x1,
            [Description("Start Y pixel in the latest screenshot.")] int y1,
            [Description("End X pixel in the latest screenshot.")] int x2,
            [Description("End Y pixel in the latest screenshot.")] int y2,
            [Description("Intermediate move steps (2-50, default 10).")] int steps = 10,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            ActivityLogger.LogPending("nexora_browser_drag", $"({x1},{y1})->({x2},{y2})", "");
            try
            {
                var result = await s.DragAsync(x1, y1, x2, y2, steps, ct).ConfigureAwait(false);
                ActivityLogger.CompletePending("nexora_browser_drag", $"({x1},{y1})->({x2},{y2})", result.Result, "", success: result.Success, error: result.Success ? null : result.Result);
                return Json(result);
            }
            catch (Exception ex)
            {
                ActivityLogger.CompletePending("nexora_browser_drag", $"({x1},{y1})->({x2},{y2})", "", "", success: false, error: ex.Message);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [McpServerTool(Name = "nexora_browser_go_back")]
        [Description("Go back one page in history (like the browser back button).")]
        public static async Task<string> GoBackAsync(
            IServiceProvider services,
            [Description("Navigation timeout in milliseconds.")] int timeoutMs = 15000,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            try
            {
                var result = await s.GoBackAsync(timeoutMs, ct).ConfigureAwait(false);
                return Json(result);
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [McpServerTool(Name = "nexora_browser_go_forward")]
        [Description("Go forward one page in history (like the browser forward button).")]
        public static async Task<string> GoForwardAsync(
            IServiceProvider services,
            [Description("Navigation timeout in milliseconds.")] int timeoutMs = 15000,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            try
            {
                var result = await s.GoForwardAsync(timeoutMs, ct).ConfigureAwait(false);
                return Json(result);
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [McpServerTool(Name = "nexora_browser_reload")]
        [Description("Reload the current page.")]
        public static async Task<string> ReloadAsync(
            IServiceProvider services,
            [Description("Navigation timeout in milliseconds.")] int timeoutMs = 30000,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            try
            {
                var result = await s.ReloadAsync(timeoutMs, ct).ConfigureAwait(false);
                return Json(result);
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [McpServerTool(Name = "nexora_browser_set_viewport")]
        [Description("WEBSITE/RENDERING TESTING: emulate a viewport size (responsive testing — e.g. 390x844 mobile, 1366x768 desktop). Pass width=0/height=0 to reset to the real window size. Returns the actual rendered size so you can verify. Screenshot after changing to see the rendering.")]
        public static async Task<string> SetViewportAsync(
            IServiceProvider services,
            [Description("Viewport width in CSS pixels (0 = reset).")] int width,
            [Description("Viewport height in CSS pixels (0 = reset).")] int height,
            [Description("Optional device pixel ratio override (0 = keep default).")] double scale = 0,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            ActivityLogger.LogPending("nexora_browser_set_viewport", $"{width}x{height}", "");
            try
            {
                var result = await s.SetViewportAsync(width, height, scale, ct).ConfigureAwait(false);
                ActivityLogger.CompletePending("nexora_browser_set_viewport", $"{width}x{height}", $"{result.Width}x{result.Height}", "", success: result.Success, error: result.Success ? null : result.Error);
                return Json(result);
            }
            catch (Exception ex)
            {
                ActivityLogger.CompletePending("nexora_browser_set_viewport", $"{width}x{height}", "", "", success: false, error: ex.Message);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [McpServerTool(Name = "nexora_browser_console")]
        [Description("WEBSITE TESTING: read buffered browser console messages and page errors (console.log/warn/error, uncaught exceptions, unhandled rejections) produced since the agent started interacting with this page, then clears the buffer. Use after navigating/clicking/typing to catch JS errors and rendering failures. Note: logs emitted during page parse before any agent interaction are not captured.")]
        public static async Task<string> ConsoleAsync(
            IServiceProvider services,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            try
            {
                var result = await s.GetConsoleAsync(ct).ConfigureAwait(false);
                return Json(result);
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [McpServerTool(Name = "nexora_browser_upload")]
        [Description("Upload a local file into a <input type=file> element (no file dialog needed). The file must exist on the machine running the NEXORA MCP app.")]
        public static async Task<string> UploadAsync(
            IServiceProvider services,
            [Description("CSS selector of the file input element.")] string selector,
            [Description("Full local path of the file to upload, e.g. C:\\Temp\\photo.png.")] string filePath,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            ActivityLogger.LogPending("nexora_browser_upload", $"{selector} <- {filePath}", "");
            try
            {
                var result = await s.UploadAsync(selector, filePath, ct).ConfigureAwait(false);
                ActivityLogger.CompletePending("nexora_browser_upload", selector, result.Result, "", success: result.Success, error: result.Success ? null : result.Result);
                return Json(result);
            }
            catch (Exception ex)
            {
                ActivityLogger.CompletePending("nexora_browser_upload", selector, "", "", success: false, error: ex.Message);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [McpServerTool(Name = "nexora_browser_select_option")]
        [Description("Select an option in a <select> dropdown by its value attribute or visible text (fires input+change events like a human).")]
        public static async Task<string> SelectOptionAsync(
            IServiceProvider services,
            [Description("CSS selector of the <select> element.")] string selector,
            [Description("Option value attribute to select (preferred).")] string? value = null,
            [Description("Visible option text to select (fallback if value omitted).")] string? label = null,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            try
            {
                var result = await s.SelectOptionAsync(selector, string.IsNullOrWhiteSpace(value) ? null : value, string.IsNullOrWhiteSpace(label) ? null : label, ct).ConfigureAwait(false);
                return Json(result);
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [McpServerTool(Name = "nexora_browser_snapshot")]
        [Description("ELEMENT DISCOVERY: dump a compact map of every interactive element on the page — index, role/tag, visible text, and a ready-to-use CSS selector per line. Use it to find selectors for click/fill, or use the index with nothing else needed. Cheaper than a screenshot for finding elements.")]
        public static async Task<string> SnapshotAsync(
            IServiceProvider services,
            [Description("Maximum elements to include (20-1000, default 300).")] int maxNodes = 300,
            CancellationToken ct = default)
        {
            var s = GetSession(services);
            try
            {
                var result = await s.SnapshotAsync(maxNodes, ct).ConfigureAwait(false);
                return Json(result);
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }
    }
}