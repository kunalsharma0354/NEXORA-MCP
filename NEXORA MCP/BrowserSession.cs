using System.Text.Json;

namespace NEXORA_MCP
{
    // ════════════════════════════════════════════════════════════════════════
    //  BROWSER SESSION (MCP side)
    //  "Session" over the loopback bridge → the browser that is embedded
    //  INSIDE the NEXORA MCP app (the user's dashboard window) is the exact
    //  browser the AI drives. No separate Chromium, no Playwright, nothing to
    //  install. Profile persists on disk so Gmail etc. logins survive restarts.
    //
    //  If the app isn't running, every method returns a clear message telling
    //  the agent to ask the user to open the NEXORA MCP app.
    // ════════════════════════════════════════════════════════════════════════

    public sealed class BrowserSession : IAsyncDisposable
    {
        private readonly BridgeClient _client;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public string StatusFile => _client.TryGetPort() is int p ? $"bridge port {p}" : "offline";

        public BrowserSession()
        {
            _client = BridgeClient.FromNexoraRoot();
        }

        public bool IsRunning => _client.TryGetPort() is > 0;

        public async Task<LaunchResult> EnsureLaunchedAsync(string? proxyServer = null, string? proxyUsername = null, string? proxyPassword = null, CancellationToken ct = default)
        {
            return await Gate(async () =>
            {
                var result = await _client.CallAsync<LaunchResult>("launch", new
                {
                    proxyServer, proxyUsername, proxyPassword
                }, ct).ConfigureAwait(false);
                ActivityLogger.LogEvent("browser", "Browser ready (embedded in app).");
                return result;
            }, ct).ConfigureAwait(false);
        }

        public async Task<LaunchResult> CloseAsync(CancellationToken ct = default)
        {
            return await Gate(() => _client.CallAsync<LaunchResult>("close", null, ct), ct).ConfigureAwait(false);
        }

        public async Task<NavResult> NavigateAsync(string url, string waitUntil = "load", int timeoutMs = 60000, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<NavResult>("navigate", new { url, waitUntil, timeoutMs }, ct), ct).ConfigureAwait(false);
        }

        public async Task<ContentResult> GetContentAsync(string format = "text", string? selector = null, int maxChars = 50000, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<ContentResult>("get_content", new { format, selector, maxChars }, ct), ct).ConfigureAwait(false);
        }

        public async Task<ScreenshotResult> TakeScreenshotAsync(bool fullPage = false, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<ScreenshotResult>("screenshot", new { fullPage }, ct), ct).ConfigureAwait(false);
        }

        public async Task<ActionResult> ClickAsync(string selector, string button = "left", int timeoutMs = 30000, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<ActionResult>("click", new { selector, button, timeoutMs }, ct), ct).ConfigureAwait(false);
        }

        public async Task<ActionResult> FillAsync(string selector, string value, bool force = false, int timeoutMs = 30000, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<ActionResult>("fill", new { selector, value, timeoutMs }, ct), ct).ConfigureAwait(false);
        }

        public async Task<ActionResult> TypeAsync(string selector, string text, int delayMs = 50, int timeoutMs = 30000, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<ActionResult>("type", new { selector, text, delayMs }, ct), ct).ConfigureAwait(false);
        }

        public async Task<ActionResult> PressKeyAsync(string key, string? selector = null, int timeoutMs = 30000, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<ActionResult>("press_key", new { key, selector }, ct), ct).ConfigureAwait(false);
        }

        public async Task<ActionResult> EvaluateAsync(string script, string? argJson = null, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<ActionResult>("evaluate", new { script }, ct), ct).ConfigureAwait(false);
        }

        public async Task<WaitResult> WaitForAsync(string state = "networkidle", int timeoutMs = 30000, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<WaitResult>("wait_for", new { state, timeoutMs }, ct), ct).ConfigureAwait(false);
        }

        public async Task<List<TabInfo>> GetTabsAsync(CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<List<TabInfo>>("get_tabs", null, ct), ct).ConfigureAwait(false);
        }

        public async Task<TabInfo> OpenTabAsync(string? url = null, string waitUntil = "load", int timeoutMs = 60000, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<TabInfo>("open_tab", new { url, waitUntil, timeoutMs }, ct), ct).ConfigureAwait(false);
        }

        public async Task<ActionResult> SwitchTabAsync(int index, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<ActionResult>("switch_tab", new { index }, ct), ct).ConfigureAwait(false);
        }

        public async Task<ActionResult> CloseTabAsync(int? index = null, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<ActionResult>("close_tab", new { index }, ct), ct).ConfigureAwait(false);
        }

        public async Task<SetProxyResult> SetProxyAsync(string? server, string? username, string? password, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<SetProxyResult>("set_proxy", new { server, username, password }, ct), ct).ConfigureAwait(false);
        }

        // ── Ultra pack: virtual cursor, scroll/drag, history, testing ──
        public async Task<ActionResult> ClickAtAsync(int x, int y, string button = "left", int clicks = 1, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<ActionResult>("click_at", new { x, y, button, clicks }, ct), ct).ConfigureAwait(false);
        }

        public async Task<ActionResult> HoverAtAsync(int x, int y, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<ActionResult>("hover_at", new { x, y }, ct), ct).ConfigureAwait(false);
        }

        public async Task<ActionResult> ScrollAsync(string direction = "down", int pixels = 600, int x = -1, int y = -1, string? selector = null, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<ActionResult>("scroll", new { direction, pixels, x, y, selector }, ct), ct).ConfigureAwait(false);
        }

        public async Task<ActionResult> DragAsync(int x1, int y1, int x2, int y2, int steps = 10, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<ActionResult>("drag", new { x1, y1, x2, y2, steps }, ct), ct).ConfigureAwait(false);
        }

        public async Task<ActionResult> GoBackAsync(int timeoutMs = 15000, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<ActionResult>("go_back", new { timeoutMs }, ct), ct).ConfigureAwait(false);
        }

        public async Task<ActionResult> GoForwardAsync(int timeoutMs = 15000, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<ActionResult>("go_forward", new { timeoutMs }, ct), ct).ConfigureAwait(false);
        }

        public async Task<ActionResult> ReloadAsync(int timeoutMs = 30000, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<ActionResult>("reload", new { timeoutMs }, ct), ct).ConfigureAwait(false);
        }

        public async Task<ViewportResult> SetViewportAsync(int width, int height, double scale = 0, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<ViewportResult>("set_viewport", new { width, height, scale }, ct), ct).ConfigureAwait(false);
        }

        public async Task<ConsoleResult> GetConsoleAsync(CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<ConsoleResult>("console", null, ct), ct).ConfigureAwait(false);
        }

        public async Task<ActionResult> UploadAsync(string selector, string filePath, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<ActionResult>("upload", new { selector, filePath }, ct), ct).ConfigureAwait(false);
        }

        public async Task<ActionResult> SelectOptionAsync(string selector, string? value = null, string? label = null, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<ActionResult>("select_option", new { selector, value, label }, ct), ct).ConfigureAwait(false);
        }

        public async Task<SnapshotResult> SnapshotAsync(int maxNodes = 300, CancellationToken ct = default)
        {
            return await Gate(() =>
                _client.CallAsync<SnapshotResult>("snapshot", new { maxNodes }, ct), ct).ConfigureAwait(false);
        }

        public LaunchResult Status()
        {
            var port = _client.TryGetPort();
            if (port == null || port <= 0)
            {
                return new LaunchResult(false, BridgeClient.NeedAppMessage, "", "", 0, null, "Browser offline — open the NEXORA MCP app.");
            }
            try
            {
                return _client.CallAsync<LaunchResult>("status", null, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                return new LaunchResult(false, BridgeClient.NeedAppMessage, "", "", 0, null, ex.Message);
            }
        }

        private async Task<T> Gate<T>(Func<Task<T>> action, CancellationToken ct)
        {
            await _gate.WaitAsync(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
            try
            {
                return await action().ConfigureAwait(false);
            }
            finally { _gate.Release(); }
        }

        public async ValueTask DisposeAsync()
        {
            _gate.Dispose();
            await Task.CompletedTask;
        }
    }

    public sealed record LaunchResult(bool IsRunning, string ProfilePath, string ActivePageUrl, string Title, int TabsCount, string? Proxy, string Error);
    public sealed record NavResult(bool Success, string Url, string Title, int LoadTimeMs, string Error);
    public sealed record ContentResult(bool Success, string Content, bool Truncated, string Url, string Title, string Error);
    public sealed record ScreenshotResult(bool Success, string DataUrl, string FilePath, int ByteSize, int WidthPx, int HeightPx, int ViewportW, int ViewportH);
    public sealed record ActionResult(bool Success, string Url, string Title, string Result);
    public sealed record WaitResult(bool Success, string Url, string Title, string Error);
    public sealed record SetProxyResult(bool Success, bool Restarted, string ActivePageUrl);
    public sealed record TabInfo(int Index, string Url, string Title, bool IsActive);
    public sealed record ViewportResult(bool Success, int Width, int Height, double DeviceScaleFactor, string Error);
    public sealed record ConsoleMsg(string Type, string Message, string Url);
    public sealed record ConsoleResult(bool Success, List<ConsoleMsg> Messages, int Count, string Error);
    public sealed record SnapshotResult(bool Success, string Snapshot, int NodeCount, bool Truncated, string Error);

    internal sealed class BrowserStoppedException : Exception
    {
        public BrowserStoppedException(string message) : base(message) { }
    }
}