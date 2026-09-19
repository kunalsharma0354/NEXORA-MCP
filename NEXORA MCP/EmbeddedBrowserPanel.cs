using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace NEXORA_MCP
{
    // ════════════════════════════════════════════════════════════════════════
    //  EMBEDDED BROWSER — everything inside the Nexora app.
    //
    //  WebView2 (the OS-installed Edge/Chromium runtime — present on every
    //  Windows machine, NOT a separately-installed browser) is hosted INSIDE
    //  the Nexora dashboard window. There is no separate Chrome window, no
    //  Playwright, nothing to install.
    //
    //  The GUI hosts this panel and ALSO runs a tiny loopback HTTP bridge on
    //  127.0.0.1. The MCP server (the --mcp process OpenCode spawns) drives
    //  the very same embedded browser by calling that bridge. The user sees
    //  and controls the browser right inside the app window (address bar is
    //  theirs to use) and the AI agent controls the identical WebView2.
    //
    //  Profile persists (UserDataFolder) so Gmail etc. logins survive
    //  restarts — the user logs in once.
    // ════════════════════════════════════════════════════════════════════════

    public sealed class EmbeddedBrowserPanel : UserControl
    {
        private const string ProfileDirName = "BrowserProfile";
        private const string ScreenshotsDirName = "Screenshots";
        private const string StatusFileName = "browser_status.json";

        private readonly string _rootDir;
        private readonly string _profileDir;
        private readonly string _screenshotDir;
        private readonly string _statusFile;

private Panel _toolbar = null!;
        private TextBox _address = null!;
        private Button _btnBack = null!;
        private Button _btnFwd = null!;
        private Button _btnRefresh = null!;

        // ── Multi-tab engine: one WebView2 per tab, all sharing the same
        // profile (UserDataFolder). Only the active tab is Visible (a hidden
        // WebView2 stops rendering, so screenshots/clicks always target the
        // visible active tab). `_webView` stays as a property so every
        // existing reference keeps working, routed to the active tab.
        private sealed class Tab
        {
            public WebView2 View = null!;
        }
        private readonly List<Tab> _tabs = new();
        private int _activeTab;
        private CoreWebView2Environment? _sharedEnv;
        private string? _envProxy;
        private WebView2 _webView => _tabs[_activeTab].View;
        private CoreWebView2? ActiveCore => _tabs.Count == 0 ? null : _tabs[_activeTab].View.CoreWebView2;

        // Last screenshot metrics — the agent's click_at/hover/drag/scroll
        // coordinates are in screenshot pixels; these map them to CSS pixels.
        private int _shotW, _shotH, _shotCssW, _shotCssH;

        private LoopbackBridgeServer? _server;
        private System.Windows.Forms.Timer? _navTimer;

        public string? ProxyServer { get; private set; }
        public string? ProxyUsername { get; private set; }
        public string? ProxyPassword { get; private set; }

        public int BridgePort => _server?.Port ?? 0;
        public string UserDataFolder => _profileDir;
        public string ScreenshotDir => _screenshotDir;

        public EmbeddedBrowserPanel()
        {
            _rootDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nexora");
            _profileDir = Path.Combine(_rootDir, ProfileDirName);
            _screenshotDir = Path.Combine(_rootDir, ScreenshotsDirName);
            _statusFile = Path.Combine(_rootDir, StatusFileName);
            Directory.CreateDirectory(_profileDir);
            Directory.CreateDirectory(_screenshotDir);

            BuildUi();
            Load += async (_, _) => await EnsureInitializedAsync().ConfigureAwait(true);
            HandleDestroyed += (_, _) => StopBridge();
        }

/// <summary>Idempotent: creates the WebView2 core + starts the loopback
        /// bridge so the MCP agent can drive this browser even before the user
        /// toggles the panel into view. Must be called on the UI thread.</summary>
        private Task? _initTask;
        public Task EnsureInitializedAsync() => _initTask ??= EnsureInitializedCoreAsync();

        private async Task EnsureInitializedCoreAsync()
        {
            if (_webView.CoreWebView2 != null && _server != null) return;
            try
            {
if (_webView.CoreWebView2 == null)
                {
                    await EnsureCoreAsync(null, null, null);
                    _navTimer = new System.Windows.Forms.Timer { Interval = 600 };
                    _navTimer.Tick += (_, _) =>
                    {
                        // Sync the bar with the live page — but NEVER while the user
                        // is typing in it (that stomps their input back to the
                        // current URL, e.g. about:blank).
                        var core = _webView.CoreWebView2;
                        if (core == null || _address.Focused) return;
                        var src = core.Source ?? "";
                        if (_address.Text != src) _address.Text = src;
                    };
                    _navTimer.Start();
                }
                StartBridge();
                WriteStatusFile();
                // Preinstalled search engine: start on Google instead of a blank
                // page (only when fresh — never clobbers a live page). Runs
                // AFTER the bridge is up so a slow network never blocks MCP.
                var homeCore = _webView.CoreWebView2;
                if (homeCore != null && (string.IsNullOrWhiteSpace(homeCore.Source) ||
                        homeCore.Source.StartsWith("about:", StringComparison.OrdinalIgnoreCase)))
                    {
                        try { homeCore.Navigate(GoogleHome); } catch { }
                    }
            }
catch (Exception ex)
            {
                ActivityLogger.LogEvent("browser", $"WebView init failed: {ex.Message}");
            }
        }

        private void BuildUi()
        {
            DoubleBuffered = true;
            BackColor = UITheme.Back;

            _toolbar = new Panel
            {
                BackColor = UITheme.TopBar,
                Dock = DockStyle.Top,
                Height = 48
            };
_toolbar.Paint += (_, e) =>
            {
                using var pen = new Pen(UITheme.Border);
                e.Graphics.DrawLine(pen, 0, _toolbar.Height - 1, _toolbar.Width, _toolbar.Height - 1);
            };

            _btnBack = ToolbarButton("\u2190"); _btnBack.Click += (_, _) => Safe(() => _webView.CoreWebView2?.GoBack());
            _btnFwd = ToolbarButton("\u2192"); _btnFwd.Click += (_, _) => Safe(() => _webView.CoreWebView2?.GoForward());
            _btnRefresh = ToolbarButton("\u21BB"); _btnRefresh.Click += (_, _) => Safe(() => _webView.CoreWebView2?.Reload());

            _address = new TextBox
            {
                Font = UITheme.Body(10),
                ForeColor = UITheme.TextPrimary,
                BackColor = UITheme.Field,
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = false,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            _address.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; NavigateFromAddress(); }
            };

            var btnGo = ToolbarButton("GO", 58);
            btnGo.Click += (_, _) => NavigateFromAddress();

            _toolbar.Controls.AddRange(new Control[] { _btnBack, _btnFwd, _btnRefresh, btnGo, _address });
            _toolbar.Resize += (_, _) =>
            {
                _btnBack.Location = new Point(8, 9);
                _btnFwd.Location = new Point(_btnBack.Right + 4, 9);
                _btnRefresh.Location = new Point(_btnFwd.Right + 4, 9);
                _address.Location = new Point(_btnRefresh.Right + 10, 8);
                _address.Width = Math.Max(120, _toolbar.Width - _btnRefresh.Right - 10 - 58 - 10);
                btnGo.Location = new Point(_address.Right + 6, 8);
            };

// First tab view (core is initialized async in EnsureInitializedAsync).
            var first = new WebView2 { Dock = DockStyle.Fill, BackColor = UITheme.Back };
            Controls.Add(first);
            _tabs.Add(new Tab { View = first });
            _activeTab = 0;
            first.Visible = true;
            first.BringToFront();
            _toolbar.BringToFront();

            Controls.AddRange(new Control[] { _toolbar });
        }

        private Button ToolbarButton(string glyph, int width = 30)
        {
            var b = new Button
            {
                Text = glyph,
                Font = UITheme.Body(10),
                ForeColor = UITheme.TextSecondary,
                BackColor = UITheme.Card,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(width, 30),
                TabStop = false,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = UITheme.Border;
            b.FlatAppearance.BorderSize = 1;
            b.MouseEnter += (_, _) => { b.BackColor = UITheme.CardHover; b.ForeColor = UITheme.TextPrimary; };
            b.MouseLeave += (_, _) => { b.BackColor = UITheme.Card; b.ForeColor = UITheme.TextSecondary; };
            return b;
        }

        private static void Safe(Action a) { try { a(); } catch { } }

private const string GoogleHome = "https://www.google.com";
        private const string GoogleSearch = "https://www.google.com/search?q=";

        /// <summary>Omnibox: real URLs navigate directly, everything else
        /// becomes a Google search (the preinstalled search engine).</summary>
        private static string NormalizeAddress(string? raw)
        {
            raw = (raw ?? "").Trim();
            if (raw.Length == 0) return "";
            // Already a full URL.
            if (raw.Contains("://", StringComparison.OrdinalIgnoreCase)) return raw;
            // Non-hierarchical scheme: data:, about:, blob:, file:, edge:, ...
            int colon = raw.IndexOf(':');
            if (colon > 0 && colon <= 12)
            {
                var scheme = raw[..colon];
                if (char.IsLetter(scheme[0]) && scheme.All(c => char.IsLetterOrDigit(c) || c is '+' or '-' or '.'))
                    return raw;
            }
            // Looks like a host: domain, localhost, host:port, IP.
            if (!raw.Contains(' ') && (raw.Contains('.') || raw.Contains(':') ||
                raw.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
                return "https://" + raw;
            // Otherwise: search it on Google.
            return GoogleSearch + Uri.EscapeDataString(raw);
        }

        private void NavigateFromAddress()
        {
            var url = NormalizeAddress(_address.Text);
            if (url.Length == 0) return;
            Safe(() => _webView.CoreWebView2?.Navigate(url));
        }

        // ════════════════════════════════════════════════════════════════════
        //  CORE INIT / PROXY (proxy bound at env creation; change = rebuild)
        // ════════════════════════════════════════════════════════════════════

public async Task EnsureCoreAsync(string? proxyServer, string? username, string? password)
        {
            ProxyServer = proxyServer; ProxyUsername = username; ProxyPassword = password;
            if (_tabs.Count == 0)
            {
                var v = new WebView2 { Dock = DockStyle.Fill, BackColor = UITheme.Back, Visible = true };
                Controls.Add(v);
                _tabs.Add(new Tab { View = v });
                _activeTab = 0;
                _toolbar.BringToFront();
            }
            if (_webView.CoreWebView2 == null)
                await InitTabCoreAsync(_webView).ConfigureAwait(true);
            ActivityLogger.LogEvent("browser", "Embedded browser core ready. Profile: " + _profileDir);
        }

        // Shared env (one per proxy config) so every tab reuses it.
        private async Task<CoreWebView2Environment> GetOrCreateEnvAsync()
        {
            var want = ProxyServer ?? "";
            if (_sharedEnv != null && string.Equals(_envProxy ?? "", want, StringComparison.OrdinalIgnoreCase))
                return _sharedEnv;

            string? browserArgs = null;
            if (!string.IsNullOrWhiteSpace(ProxyServer))
            {
                var u = new Uri(ProxyServer);
                var scheme = u.Scheme switch { "socks5" => "socks5://", "socks4" => "socks4://", _ => "http://" };
                browserArgs = $"--proxy-server={scheme}{u.Host}:{u.Port} --proxy-bypass-list=<-loopback>";
            }

            var options = browserArgs != null
                ? new CoreWebView2EnvironmentOptions(null, null, browserArgs)
                : new CoreWebView2EnvironmentOptions();
            _sharedEnv = await CoreWebView2Environment.CreateAsync(null, _profileDir, options);
            _envProxy = ProxyServer;
            return _sharedEnv;
        }

// Console capture hook. NOTE (hard-won): WebView2 silently drops ONE big
        // ExecuteScriptAsync payload right after navigation (returns success,
        // defines nothing). Small chunked evals stick reliably — so the hook
        // is installed in 3 small pieces, never one big blob.
        private const string HookCore1 =
            "window.__nxCon=window.__nxCon||[];"
            + "window.__nxPush=function(t,a){try{var m=[];for(var i=0;i<a.length;i++){try{m.push(typeof a[i]==='object'?JSON.stringify(a[i]):String(a[i]))}catch(e){m.push(String(a[i]))}}"
            + "window.__nxCon.push({t:t,m:m.join(' '),u:location.href});if(window.__nxCon.length>200)window.__nxCon.splice(0,window.__nxCon.length-200)}catch(e){}};";
        private const string HookCore2 =
            "window.__nxEnsure=function(){var diag=[];try{"
            + "if(!window.console) return 'noconsole';"
            + "if(!console.log) return 'nolog';"
            + "if(console.log.__nx){window.__nxInstalled=true;return 'already';}"
            + "var methods=['log','info','warn','error','debug'];"
            + "for(var i=0;i<methods.length;i++){var k=methods[i];try{if(!console[k]){diag.push(k+':missing');continue;}var o=console[k].bind(console);"
            + "var f=(function(kk,oo){return function(){window.__nxPush(kk,arguments);return oo.apply(null,arguments)}})(k,o);"
            + "f.__nx=true;console[k]=f;diag.push(k+'='+((console[k]&&console[k].__nx===true)?'ok':'STUCK'));}catch(e){diag.push(k+':ERR:'+e.message)}}"
            + "window.__nxInstalled=!!(console.log&&console.log.__nx);"
            + "return diag.join(',');}catch(e){return 'FATAL:'+e.message}};";
        private const string HookCore3 =
            "window.__nxEnsure();"
            + "try{document.addEventListener('DOMContentLoaded',function(){window.__nxEnsure()});}catch(e){}"
            + "try{window.addEventListener('load',function(){window.__nxEnsure()});}catch(e){}"
            + "try{window.addEventListener('error',function(e){window.__nxPush('pageerror',[e.message+' @ '+(e.filename||'')+':'+(e.lineno||'')])});}catch(e){}"
            + "try{window.addEventListener('unhandledrejection',function(e){window.__nxPush('pageerror',['Unhandled rejection: '+((e.reason&&e.reason.message)||e.reason)])});}catch(e){}";
        private const string ConsoleHookJs = "(function(){if(window.__nxInstalled&&window.__nxCon)return;"
            + HookCore1 + HookCore2 + HookCore3 + "})();";

        // Idempotent: guarantees the console override is live in the current
        // document before interactions (and before reading the buffer).
        // SINGLE eval only (idempotent guard inside makes re-runs cheap).
        // Never chain dependent evals back-to-back: WebView2 silently drops
        // definitions from rapid successive ExecuteScriptAsync calls — the
        // install and its use must ride in the SAME eval (see DoEvaluateAsync)
        // or in separate ops seconds apart (normal MCP round trips).
        private async Task EnsureConsoleHookAsync()
        {
            try { await EvalAsync("(function(){if(window.__nxInstalled&&window.__nxCon)return;" + HookCore1 + HookCore2 + HookCore3 + "})()").ConfigureAwait(true); } catch { }
        }

        // Console capture is ensured live before every interaction and before
        // every read, so it covers all agent-triggered activity. NOTE: logs
        // emitted during page *parse* (before any agent interaction) cannot be
        // captured — WebView2 does not reliably run document-created scripts.

        private async Task InitTabCoreAsync(WebView2 view)
        {
            var env = await GetOrCreateEnvAsync().ConfigureAwait(true);
            await view.EnsureCoreWebView2Async(env).ConfigureAwait(true);
            var core = view.CoreWebView2;
            if (core == null) throw new InvalidOperationException("WebView2 core failed to initialize.");

            core.Settings.AreDevToolsEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            try { await core.AddScriptToExecuteOnDocumentCreatedAsync(ConsoleHookJs).ConfigureAwait(true); } catch { }
            core.NewWindowRequested += OnNewWindowRequested;

            if (!string.IsNullOrWhiteSpace(ProxyUsername) || !string.IsNullOrWhiteSpace(ProxyPassword))
            {
                var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ProxyUsername}:{ProxyPassword}"));
                core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                core.WebResourceRequested += (_, args) =>
                {
                    try { args.Request.Headers.SetHeader("Proxy-Authorization", "Basic " + basic); }
                    catch { }
                };
            }
        }

        private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            // Popups / target=_blank links become real tabs.
            var deferral = e.GetDeferral();
            _ = OpenPopupAsync(e, deferral);
        }

        private async Task OpenPopupAsync(CoreWebView2NewWindowRequestedEventArgs e, CoreWebView2Deferral deferral)
        {
            try
            {
                var tab = await AddTabAsync(null).ConfigureAwait(true);
                e.NewWindow = tab.View.CoreWebView2;
                e.Handled = true;
            }
            catch { e.Handled = false; }
            finally { try { deferral.Complete(); } catch { } }
        }

public async Task ApplyProxyAsync(string? server, string? username, string? password)
        {
            ProxyServer = server; ProxyUsername = username; ProxyPassword = password;
            _sharedEnv = null; // force env rebuild with the new proxy args

            var urls = _tabs.Select(t => t.View.CoreWebView2?.Source ?? "").ToList();
            if (urls.Count == 0) urls.Add("");

            // The old WebView2 controls remove themselves from Controls when
            // disposed; add the new ones, keeping them z-ordered above the
            // dashboard content but below the toolbar.
            foreach (var t in _tabs)
            {
                try { t.View.Dispose(); } catch { }
            }
            _tabs.Clear();

            for (int i = 0; i < urls.Count; i++)
            {
                var view = new WebView2 { Dock = DockStyle.Fill, BackColor = UITheme.Back, Visible = i == _activeTab };
                Controls.Add(view);
                _tabs.Add(new Tab { View = view });
                await InitTabCoreAsync(view).ConfigureAwait(true);
            }
            if (_activeTab >= _tabs.Count) _activeTab = 0;
            _toolbar.BringToFront();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<CoreWebView2NavigationCompletedEventArgs>? h = null;
            h = (_, _) => { tcs.TrySetResult(true); try { _webView.NavigationCompleted -= h; } catch { } };
            _webView.NavigationCompleted += h;

            var back = urls[Math.Min(_activeTab, urls.Count - 1)];
            if (!string.IsNullOrEmpty(back))
            {
                _webView.CoreWebView2?.Navigate(back);
                await Task.WhenAny(tcs.Task, Task.Delay(30000)).ConfigureAwait(true);
            }
            WriteStatusFile();
        }

        // ── Tab management ───────────────────────────────────────────────────
        private async Task<Tab> AddTabAsync(string? url)
        {
            var view = new WebView2 { Dock = DockStyle.Fill, BackColor = UITheme.Back, Visible = false };
            Controls.Add(view);
            var tab = new Tab { View = view };
            _tabs.Add(tab);
            await InitTabCoreAsync(view).ConfigureAwait(true);
            var target = string.IsNullOrWhiteSpace(url) ? GoogleHome : NormalizeAddress(url);
            if (target.Length > 0) view.CoreWebView2?.Navigate(target);
            ActivateTab(_tabs.Count - 1);
            return tab;
        }

        private void ActivateTab(int index)
        {
            if (index < 0 || index >= _tabs.Count) throw new InvalidOperationException($"No tab at index {index} (have {_tabs.Count}).");
            for (int i = 0; i < _tabs.Count; i++) _tabs[i].View.Visible = (i == index);
            _activeTab = index;
            _tabs[index].View.BringToFront();
            _toolbar.BringToFront();
            WriteStatusFile();
        }

        private static async Task<string> TitleOfAsync(CoreWebView2 core)
        {
            try
            {
                var raw = await core.ExecuteScriptAsync("document.title").ConfigureAwait(true);
                var title = EmbeddedBrowserPanel.UnwrapJsString(raw);
                return string.IsNullOrWhiteSpace(title) ? (core.DocumentTitle ?? "") : title;
            }
            catch { try { return core.DocumentTitle ?? ""; } catch { return ""; } }
        }

        private async Task<bool> WaitNavAsync(WebView2 view, int timeoutMs)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<CoreWebView2NavigationCompletedEventArgs>? h = null;
            h = (_, _) => { tcs.TrySetResult(true); try { view.NavigationCompleted -= h; } catch { } };
            view.NavigationCompleted += h;
            var done = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs)).ConfigureAwait(true) == tcs.Task;
            try { view.NavigationCompleted -= h; } catch { }
            return done;
        }

        // Subscribe first, then act — no missed completions.
        private async Task<bool> WaitNavAsync(WebView2 view, Func<Task> act, int timeoutMs)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<CoreWebView2NavigationCompletedEventArgs>? h = null;
            h = (_, _) => { tcs.TrySetResult(true); try { view.NavigationCompleted -= h; } catch { } };
            view.NavigationCompleted += h;
            try { await act().ConfigureAwait(true); } catch { }
            var done = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs)).ConfigureAwait(true) == tcs.Task;
            try { view.NavigationCompleted -= h; } catch { }
            return done;
        }

        // ════════════════════════════════════════════════════════════════════
        //  CDP ATOMICS (must run on the UI thread — bridge marshals in)
        // ════════════════════════════════════════════════════════════════════

        private async Task<string> CdpAsync(string method, string? paramsJson = null)
        {
            var core = _webView.CoreWebView2
                ?? throw new InvalidOperationException("Embedded browser is not initialized yet. Open the NEXORA MCP app first.");
            return await core.CallDevToolsProtocolMethodAsync(method, paramsJson ?? "{}");
        }

        private async Task<string> EvalAsync(string js)
        {
            var core = _webView.CoreWebView2
                ?? throw new InvalidOperationException("Embedded browser is not initialized yet. Open the NEXORA MCP app first.");
            return await core.ExecuteScriptAsync(js);
        }

        private static string J(string s) => JsonSerializer.Serialize(s);

        private async Task WaitReadyAsync(TimeSpan timeout, string expr)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                try
                {
                    var res = await EvalAsync(expr);
                    if (res == "true") return;
                }
                catch { }
                await Task.Delay(150);
            }
            throw new TimeoutException($"Timed out waiting for page state ({timeout.TotalSeconds:0}s).");
        }

        // ════════════════════════════════════════════════════════════════════
        //  OPERATIONS (public API used by the bridge)
        // ════════════════════════════════════════════════════════════════════

public async Task<NavResult> DoNavigateAsync(string url, string waitUntil, int timeoutMs)
        {
            url = NormalizeAddress(url);
            if (url.Length == 0) throw new ArgumentException("Provide a URL or a search query.");
var core = _webView.CoreWebView2
                ?? throw new InvalidOperationException("Embedded browser is not initialized yet. Open the NEXORA MCP app first.");
            var sw = System.Diagnostics.Stopwatch.StartNew();

            bool nav = await WaitNavAsync(_webView, () => { core.Navigate(url); return Task.CompletedTask; }, timeoutMs).ConfigureAwait(true);
            if (!nav) throw new TimeoutException($"Navigation to {url} timed out after {timeoutMs}ms.");

            if (waitUntil == "domcontentloaded")
                await WaitReadyAsync(TimeSpan.FromMilliseconds(timeoutMs),
                    "document.readyState === 'interactive' || document.readyState === 'complete'");
            else if (waitUntil != "commit")
                await WaitReadyAsync(TimeSpan.FromMilliseconds(timeoutMs), "document.readyState === 'complete'");

await Task.Delay(200);
            sw.Stop();
            WriteStatusFile();
            return new NavResult(true, await ResolveUrlAsync().ConfigureAwait(true), await GetTitleAsync() ?? "", (int)sw.ElapsedMilliseconds, "");
        }

        public string GetSource() => _webView.CoreWebView2?.Source ?? "";

        // CoreWebView2.Source is empty for some documents (e.g. data: URLs);
        // fall back to document.URL so callers still get a real address.
        private async Task<string> ResolveUrlAsync()
        {
            var src = GetSource();
            if (!string.IsNullOrEmpty(src)) return src;
            try
            {
                var raw = await EvalAsync("document.URL").ConfigureAwait(true);
                return UnwrapJsString(raw);
            }
            catch { return ""; }
        }
public async Task<string?> GetTitleAsync()
        {
            var core = ActiveCore;
            if (core == null) return "";
            return await TitleOfAsync(core).ConfigureAwait(true);
        }

        public async Task<ContentResult> DoGetContentAsync(string format, string? selector, int maxChars)
        {
            string content;
            if (format == "html")
            {
                content = string.IsNullOrWhiteSpace(selector)
                    ? await EvalAsync("(() => document.documentElement ? document.documentElement.outerHTML : '')()")
                    : await EvalAsync($"(() => {{ const el = document.querySelector({J(selector)}); return el ? el.outerHTML : ''; }})()");
            }
            else
            {
                content = string.IsNullOrWhiteSpace(selector)
                    ? await EvalAsync("(() => { const el = document.querySelector('main') || document.querySelector('article') || document.body; return el ? el.innerText : ''; })()")
                    : await EvalAsync($"(() => {{ const el = document.querySelector({J(selector)}); return el ? el.innerText : ''; }})()");
                content = UnwrapJsString(content);
            }
            var truncated = content.Length > maxChars;
            if (truncated) content = content[..maxChars] + "\n...[truncated]";
            return new ContentResult(true, content, truncated, GetSource(), await GetTitleAsync() ?? "", "");
        }

        public async Task<ScreenshotResult> DoScreenshotAsync(bool fullPage)
        {
            string result;
            try
            {
                result = fullPage
                    ? await CdpAsync("Page.captureScreenshot", "{\"format\":\"png\",\"fromSurface\":true,\"captureBeyondViewport\":true}")
                    : await CdpAsync("Page.captureScreenshot", "{\"format\":\"png\",\"fromSurface\":true}");
            }
catch
            {
                using var ms = new MemoryStream();
                await _webView.CoreWebView2!.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, ms);
                var b = ms.ToArray();
                var p = Save(b);
                var m = await MeasureShotAsync(b).ConfigureAwait(true);
                _shotW = m.w; _shotH = m.h; _shotCssW = m.cssW; _shotCssH = m.cssH;
                WriteStatusFile();
                return new ScreenshotResult(true, "data:image/png;base64," + Convert.ToBase64String(b), p, b.Length, m.w, m.h, m.cssW, m.cssH);
            }

            using var doc = JsonDocument.Parse(result);
            if (!doc.RootElement.TryGetProperty("data", out var data)) throw new InvalidOperationException("Screenshot returned no image data.");
            var bytes = Convert.FromBase64String(data.GetString() ?? "");
            var path = Save(bytes);
            var met = await MeasureShotAsync(bytes).ConfigureAwait(true);
            _shotW = met.w; _shotH = met.h; _shotCssW = met.cssW; _shotCssH = met.cssH;
            WriteStatusFile();
            return new ScreenshotResult(true, "data:image/png;base64," + Convert.ToBase64String(bytes), path, bytes.Length, met.w, met.h, met.cssW, met.cssH);
        }

        private string Save(byte[] bytes)
        {
            var fileName = $"browser_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            var filePath = Path.Combine(_screenshotDir, fileName);
            File.WriteAllBytes(filePath, bytes);
            return filePath;
        }

public async Task DoClickAsync(string selector, string button)
        {
            await EnsureConsoleHookAsync().ConfigureAwait(true);
            var rectJs =
                $"(() => {{ const el = document.querySelector({J(selector)}); if (!el) return null; " +
                "el.scrollIntoView({block:'center', behavior:'instant'}); " +
                "const r = el.getBoundingClientRect(); return {x: Math.round(r.x + r.width/2), y: Math.round(r.y + r.height/2), w: Math.floor(r.width), h: Math.floor(r.height)}; })()";
            var raw = await EvalAsync(rectJs);
            if (raw == "null" || string.IsNullOrWhiteSpace(raw)) throw new InvalidOperationException($"Element not found for selector: {selector}");

            using var doc = JsonDocument.Parse(raw);
            int x = doc.RootElement.GetProperty("x").GetInt32();
            int y = doc.RootElement.GetProperty("y").GetInt32();
            int w = doc.RootElement.TryGetProperty("w", out var wv) ? wv.GetInt32() : 0;
            int h = doc.RootElement.TryGetProperty("h", out var hv) ? hv.GetInt32() : 0;
            if (w <= 0 && h <= 0) throw new InvalidOperationException($"Element for selector is hidden (zero size): {selector}");

            string btn = button == "right" ? "right" : button == "middle" ? "middle" : "left";
            await CdpAsync("Input.dispatchMouseEvent", $"{{\"type\":\"mouseMoved\",\"x\":{x},\"y\":{y},\"buttons\":0}}");
            await CdpAsync("Input.dispatchMouseEvent", $"{{\"type\":\"mousePressed\",\"x\":{x},\"y\":{y},\"button\":\"{btn}\",\"buttons\":1,\"clickCount\":1}}");
            await CdpAsync("Input.dispatchMouseEvent", $"{{\"type\":\"mouseReleased\",\"x\":{x},\"y\":{y},\"button\":\"{btn}\",\"buttons\":0,\"clickCount\":1}}");
            WriteStatusFile();
        }

public async Task DoFillAsync(string selector, string value)
        {
            await EnsureConsoleHookAsync().ConfigureAwait(true);
            await FocusSelectorAsync(selector);
            await CdpAsync("Input.dispatchKeyEvent", "{\"type\":\"rawKeyDown\",\"key\":\"a\",\"code\":\"KeyA\",\"windowsVirtualKeyCode\":65,\"modifiers\":2}");
            await CdpAsync("Input.dispatchKeyEvent", "{\"type\":\"keyUp\",\"key\":\"a\",\"code\":\"KeyA\",\"windowsVirtualKeyCode\":65,\"modifiers\":2}");
            await CdpAsync("Input.insertText", $"{{\"text\":{J(value)}}}");
            WriteStatusFile();
        }

public async Task DoTypeAsync(string selector, string text, int delayMs)
        {
            await EnsureConsoleHookAsync().ConfigureAwait(true);
            await FocusSelectorAsync(selector);
            foreach (var ch in text)
            {
                await CdpAsync("Input.dispatchKeyEvent", $"{{\"type\":\"char\",\"text\":{J(ch.ToString())}}}");
                if (delayMs > 0) await Task.Delay(delayMs);
            }
            WriteStatusFile();
        }

        private async Task FocusSelectorAsync(string selector)
        {
            var js = $"(() => {{ const el = document.querySelector({J(selector)}); if (!el) return false; " +
                "el.scrollIntoView({block:'center', behavior:'instant'}); (el.focus ? el.focus() : null); return true; })()";
            var raw = await EvalAsync(js);
            if (raw != "true") throw new InvalidOperationException($"Element not found for selector: {selector}");
        }

public async Task DoPressKeyAsync(string key, string? selector)
        {
            await EnsureConsoleHookAsync().ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(selector))
            {
                try { await FocusSelectorAsync(selector); } catch { }
            }

            var parts = key.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            int modifiers = 0;
            string main = "";
            foreach (var p in parts)
            {
                switch (p.ToLowerInvariant())
                {
                    case "control" or "ctrl": modifiers |= 2; break;
                    case "alt": modifiers |= 1; break;
                    case "shift": modifiers |= 8; break;
                    case "meta" or "command" or "win": modifiers |= 4; break;
                    default: main = p; break;
                }
            }
            if (string.IsNullOrEmpty(main)) throw new InvalidOperationException($"Invalid key: {key}");
var mapped = KeyMap.Get(main);

            if (modifiers == 0 && mapped.IsChar)
            {
                await CdpAsync("Input.dispatchKeyEvent", $"{{\"type\":\"char\",\"text\":{J(mapped.Text)}}}");
                WriteStatusFile();
                return;
            }

            await CdpAsync("Input.dispatchKeyEvent",
                $"{{\"type\":\"rawKeyDown\",\"key\":{J(mapped.Key)},\"code\":{J(mapped.Code)},\"windowsVirtualKeyCode\":{mapped.Vk},\"modifiers\":{modifiers}}}");
await CdpAsync("Input.dispatchKeyEvent",
                $"{{\"type\":\"keyUp\",\"key\":{J(mapped.Key)},\"code\":{J(mapped.Code)},\"windowsVirtualKeyCode\":{mapped.Vk},\"modifiers\":{modifiers}}}");
            WriteStatusFile();
        }

        // ════════════════════════════════════════════════════════════════════
        //  VIRTUAL CURSOR (screenshot-pixel coordinates → page actions)
        //  Agent loop: nexora_browser_screenshot → read x/y from the image →
        //  click_at / hover_at / scroll / drag with THOSE pixels. Mapping uses
        //  the metrics stored by the last screenshot; a viewport change since
        //  then is rejected so the agent re-screenshots instead of misclicking.
        // ════════════════════════════════════════════════════════════════════

        private async Task<(int w, int h, int cssW, int cssH)> MeasureShotAsync(byte[] png)
        {
            int w = 0, h = 0;
            try { using var ms = new MemoryStream(png); using var img = System.Drawing.Image.FromStream(ms); w = img.Width; h = img.Height; } catch { }
            int cssW = 0, cssH = 0;
            try
            {
                var raw = await EvalAsync("[window.innerWidth,window.innerHeight]").ConfigureAwait(true);
                using var doc = JsonDocument.Parse(raw);
                cssW = doc.RootElement[0].GetInt32(); cssH = doc.RootElement[1].GetInt32();
            }
            catch { }
            return (w, h, cssW, cssH);
        }

        private async Task<(int x, int y)> MapShotToCssAsync(int px, int py)
        {
            if (_shotW <= 0 || _shotCssW <= 0)
                throw new InvalidOperationException("Take a screenshot first (nexora_browser_screenshot), then click/hover/drag using that screenshot's coordinates.");
            int cssW = 0, cssH = 0;
            try
            {
                var raw = await EvalAsync("[window.innerWidth,window.innerHeight]").ConfigureAwait(true);
                using var doc = JsonDocument.Parse(raw);
                cssW = doc.RootElement[0].GetInt32(); cssH = doc.RootElement[1].GetInt32();
            }
            catch { }
            if (cssW != _shotCssW || cssH != _shotCssH)
                throw new InvalidOperationException($"Viewport changed since the screenshot ({_shotCssW}x{_shotCssH} -> {cssW}x{cssH}). Take a fresh screenshot and use its coordinates.");
            return ((int)Math.Round(px * (double)_shotCssW / _shotW), (int)Math.Round(py * (double)_shotCssH / _shotH));
        }

        private static string CdpButton(string button) => button == "right" ? "right" : button == "middle" ? "middle" : "left";
        private static int CdpButtons(string button) => button == "right" ? 2 : button == "middle" ? 4 : 1;

        public async Task<string> DoClickAtAsync(int x, int y, string button, int clicks)
        {
            await EnsureConsoleHookAsync().ConfigureAwait(true);
            var (cx, cy) = await MapShotToCssAsync(x, y).ConfigureAwait(true);
            string btn = CdpButton(button);
            int cl = Math.Clamp(clicks, 1, 3);
            await CdpAsync("Input.dispatchMouseEvent", $"{{\"type\":\"mouseMoved\",\"x\":{cx},\"y\":{cy},\"buttons\":0}}").ConfigureAwait(true);
            await CdpAsync("Input.dispatchMouseEvent", $"{{\"type\":\"mousePressed\",\"x\":{cx},\"y\":{cy},\"button\":\"{btn}\",\"buttons\":{CdpButtons(button)},\"clickCount\":{cl}}}").ConfigureAwait(true);
            await CdpAsync("Input.dispatchMouseEvent", $"{{\"type\":\"mouseReleased\",\"x\":{cx},\"y\":{cy},\"button\":\"{btn}\",\"buttons\":0,\"clickCount\":{cl}}}").ConfigureAwait(true);
            WriteStatusFile();
            return $"clicked {btn} at page ({cx},{cy}) from shot ({x},{y})";
        }

        public async Task<string> DoHoverAtAsync(int x, int y)
        {
            await EnsureConsoleHookAsync().ConfigureAwait(true);
            var (cx, cy) = await MapShotToCssAsync(x, y).ConfigureAwait(true);
            await CdpAsync("Input.dispatchMouseEvent", $"{{\"type\":\"mouseMoved\",\"x\":{cx},\"y\":{cy},\"buttons\":0}}").ConfigureAwait(true);
            WriteStatusFile();
            return $"hovered page ({cx},{cy}) from shot ({x},{y})";
        }

        public async Task<string> DoScrollAsync(string direction, int pixels, int x, int y, string? selector)
        {
            await EnsureConsoleHookAsync().ConfigureAwait(true);
            int p = Math.Clamp(pixels, 10, 5000);
            var (dx, dy) = direction.ToLowerInvariant() switch
            {
                "up" => (0, -p),
                "left" => (-p, 0),
                "right" => (p, 0),
                _ => (0, p),
            };
            string pos;
            if (!string.IsNullOrWhiteSpace(selector))
            {
                var js = $"(() => {{ const el = document.querySelector({J(selector)}); if (!el) return null; " +
                    "el.scrollIntoView({block:'center', behavior:'instant'}); " +
                    $"el.scrollBy({dx}, {dy}); return [el.scrollLeft, el.scrollTop].toString(); }})()";
                var raw = await EvalAsync(js).ConfigureAwait(true);
                if (raw == "null" || string.IsNullOrWhiteSpace(raw)) throw new InvalidOperationException($"Element not found for selector: {selector}");
                pos = UnwrapJsString(raw);
            }
            else
            {
                var raw = await EvalAsync($"(window.scrollBy({dx}, {dy}), [window.scrollX, window.scrollY].toString())").ConfigureAwait(true);
                pos = UnwrapJsString(raw);
            }
            WriteStatusFile();
            return $"scrolled {direction} {p}px → scroll now {pos}";
        }

        public async Task<string> DoDragAsync(int x1, int y1, int x2, int y2, int steps)
        {
            await EnsureConsoleHookAsync().ConfigureAwait(true);
            var (ax, ay) = await MapShotToCssAsync(x1, y1).ConfigureAwait(true);
            var (bx, by) = await MapShotToCssAsync(x2, y2).ConfigureAwait(true);
            // HTML5 native drag-and-drop (draggable=true, React DnD, etc.) does
            // NOT respond to synthetic mouse events — it needs a real DnD event
            // sequence with a DataTransfer. Detect and use the JS path for it.
            var probe = await EvalAsync($"(() => {{ const el = document.elementFromPoint({ax}, {ay}); const d = el && el.closest ? (el.closest('[draggable=\"true\"]') || (el.draggable ? el : null)) : null; return d ? 'DND' : 'MOUSE'; }})()").ConfigureAwait(true);
if (UnwrapJsString(probe) == "DND")
            {
                // Walk the path like a real pointer: dragenter+dragover at
                // each step (reorder logic depends on crossing elements),
                // drop at the end. DataTransfer carries the dragged text so
                // drop handlers reading getData()/types accept the drop.
                var js = $"(() => {{ const src = document.elementFromPoint({ax}, {ay}); const dragEl = src.closest('[draggable=\"true\"]') || (src.draggable ? src : null); if (!dragEl) return 'no-src'; " +
                    "const dt = new DataTransfer(); dt.effectAllowed = 'all'; " +
                    "try { dt.setData('text/plain', (dragEl.innerText || '').trim()); dt.setData('text', (dragEl.innerText || '').trim()); } catch (e) {} " +
                    $"dragEl.dispatchEvent(new DragEvent('dragstart', {{bubbles:true, cancelable:true, dataTransfer:dt, clientX:{ax}, clientY:{ay}}})); " +
                    $"const steps = 10; let dst = null; " +
                    $"for (let i = 1; i <= steps; i++) {{ const px = {ax} + ({bx} - {ax}) * i / steps, py = {ay} + ({by} - {ay}) * i / steps; " +
                    "const el = document.elementFromPoint(px, py); if (el) { dst = el; " +
                    "el.dispatchEvent(new DragEvent('dragenter', {bubbles:true, cancelable:true, dataTransfer:dt, clientX:px, clientY:py})); " +
                    "el.dispatchEvent(new DragEvent('dragover', {bubbles:true, cancelable:true, dataTransfer:dt, clientX:px, clientY:py})); } } " +
                    $"if (dst) dst.dispatchEvent(new DragEvent('drop', {{bubbles:true, cancelable:true, dataTransfer:dt, clientX:{bx}, clientY:{by}}})); " +
                    $"dragEl.dispatchEvent(new DragEvent('dragend', {{bubbles:true, cancelable:true, dataTransfer:dt, clientX:{bx}, clientY:{by}}})); " +
                    "return 'DND-DONE'; })()";
                var raw = UnwrapJsString(await EvalAsync(js).ConfigureAwait(true));
                WriteStatusFile();
                return $"html5-dnd ({ax},{ay}) → ({bx},{by}): {raw}";
            }
            int n = Math.Clamp(steps, 2, 50);
            await CdpAsync("Input.dispatchMouseEvent", $"{{\"type\":\"mouseMoved\",\"x\":{ax},\"y\":{ay},\"buttons\":0}}").ConfigureAwait(true);
            await CdpAsync("Input.dispatchMouseEvent", $"{{\"type\":\"mousePressed\",\"x\":{ax},\"y\":{ay},\"button\":\"left\",\"buttons\":1,\"clickCount\":1}}").ConfigureAwait(true);
            // Human-paced moves: lets the renderer pump frames between events so
            // path-sensitive widgets (jQuery UI sortable etc.) track the drag
            // instead of seeing one coalesced jump.
            await Task.Delay(60).ConfigureAwait(true);
            for (int i = 1; i <= n; i++)
            {
                int mx = ax + (bx - ax) * i / n, my = ay + (by - ay) * i / n;
                await CdpAsync("Input.dispatchMouseEvent", $"{{\"type\":\"mouseMoved\",\"x\":{mx},\"y\":{my},\"buttons\":1}}").ConfigureAwait(true);
                await Task.Delay(25).ConfigureAwait(true);
            }
            await Task.Delay(60).ConfigureAwait(true);
            await CdpAsync("Input.dispatchMouseEvent", $"{{\"type\":\"mouseReleased\",\"x\":{bx},\"y\":{by},\"button\":\"left\",\"buttons\":0,\"clickCount\":1}}").ConfigureAwait(true);
            WriteStatusFile();
            return $"dragged ({ax},{ay}) → ({bx},{by})";
        }

        public async Task<string> DoHistoryAsync(string action, int timeoutMs)
        {
            var core = ActiveCore
                ?? throw new InvalidOperationException("Embedded browser is not initialized yet. Open the NEXORA MCP app first.");
            if (action == "back")
            {
                if (!core.CanGoBack) return "no back history";
                bool done = await WaitNavAsync(_webView, () => { core.GoBack(); return Task.CompletedTask; }, timeoutMs).ConfigureAwait(true);
                WriteStatusFile();
                return done ? "went back" : "back navigation timed out";
            }
            if (action == "forward")
            {
                if (!core.CanGoForward) return "no forward history";
                bool done = await WaitNavAsync(_webView, () => { core.GoForward(); return Task.CompletedTask; }, timeoutMs).ConfigureAwait(true);
                WriteStatusFile();
                return done ? "went forward" : "forward navigation timed out";
            }
            bool reloaded = await WaitNavAsync(_webView, () => { core.Reload(); return Task.CompletedTask; }, timeoutMs).ConfigureAwait(true);
            WriteStatusFile();
            return reloaded ? "reloaded" : "reload timed out";
        }

        public async Task<ViewportResult> DoViewportAsync(int width, int height, double scale)
        {
            if (width <= 0 || height <= 0)
            {
                await CdpAsync("Emulation.clearDeviceMetricsOverride").ConfigureAwait(true);
            }
            else
            {
                // deviceScaleFactor is REQUIRED by CDP — keep the live DPR unless overridden.
                double dsf = scale > 0 ? scale : 0;
                if (dsf <= 0)
                {
                    try
                    {
                        var raw = await EvalAsync("window.devicePixelRatio").ConfigureAwait(true);
                        dsf = double.Parse(UnwrapJsString(raw), System.Globalization.CultureInfo.InvariantCulture);
                    }
                    catch { }
                    if (dsf <= 0) dsf = 1;
                }
                string dsfStr = dsf.ToString(System.Globalization.CultureInfo.InvariantCulture);
                await CdpAsync("Emulation.setDeviceMetricsOverride",
                    $"{{\"width\":{width},\"height\":{height},\"deviceScaleFactor\":{dsfStr},\"mobile\":false}}").ConfigureAwait(true);
            }
            int iw = 0, ih = 0; double dpr = 0;
            try
            {
                var raw = await EvalAsync("[window.innerWidth,window.innerHeight,window.devicePixelRatio]").ConfigureAwait(true);
                using var doc = JsonDocument.Parse(raw);
                iw = doc.RootElement[0].GetInt32(); ih = doc.RootElement[1].GetInt32(); dpr = doc.RootElement[2].GetDouble();
            }
            catch { }
            WriteStatusFile();
            return new ViewportResult(true, iw, ih, dpr, "");
        }

        public async Task<ConsoleResult> DoConsoleAsync()
        {
            // Single read eval only (no chained ensure): the hook is already
            // live from pre-interaction / evaluate ensures.
            var raw = await EvalAsync("(function(){var a=(window.__nxCon||[]).slice(-100);window.__nxCon=[];return JSON.stringify(a)})()").ConfigureAwait(true);
            var msgs = new List<ConsoleMsg>();
            try
            {
                var json = UnwrapJsString(raw);
                using var doc = JsonDocument.Parse(json);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    string t = el.TryGetProperty("t", out var tv) ? tv.GetString() ?? "log" : "log";
                    string m = el.TryGetProperty("m", out var mv) ? mv.GetString() ?? "" : "";
                    string u = el.TryGetProperty("u", out var uv) ? uv.GetString() ?? "" : "";
                    msgs.Add(new ConsoleMsg(t, m, u));
                }
            }
            catch { }
            WriteStatusFile();
            return new ConsoleResult(true, msgs, msgs.Count, "");
        }

        public async Task<string> DoUploadAsync(string selector, string filePath)
        {
            await EnsureConsoleHookAsync().ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new InvalidOperationException($"File not found: {filePath}");
            var docRaw = await CdpAsync("DOM.getDocument", "{\"depth\":0}").ConfigureAwait(true);
            using var doc = JsonDocument.Parse(docRaw);
            int rootId = doc.RootElement.GetProperty("root").GetProperty("nodeId").GetInt32();
            var qRaw = await CdpAsync("DOM.querySelector", $"{{\"nodeId\":{rootId},\"selector\":{J(selector)}}}").ConfigureAwait(true);
            using var q = JsonDocument.Parse(qRaw);
            int nodeId = q.RootElement.GetProperty("nodeId").GetInt32();
            if (nodeId == 0) throw new InvalidOperationException($"Element not found for selector: {selector}");
            try
            {
                await CdpAsync("DOM.setFileInputFiles", $"{{\"files\":[{J(filePath)}],\"nodeId\":{nodeId}}}").ConfigureAwait(true);
            }
            catch (Exception ex) { throw new InvalidOperationException($"Upload failed (is the selector a file input?): {ex.Message}"); }
            WriteStatusFile();
            return $"uploaded {Path.GetFileName(filePath)} into {selector}";
        }

        public async Task<string> DoSelectAsync(string selector, string? value, string? label)
        {
            await EnsureConsoleHookAsync().ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(label))
                throw new InvalidOperationException("Provide value or label to select.");
            var js = $"(() => {{ const el = document.querySelector({J(selector)}); if (!el) return 'NOTFOUND'; " +
                "el.scrollIntoView({block:'center', behavior:'instant'}); const opts = Array.from(el.options || []); let pick = null; " +
                (string.IsNullOrWhiteSpace(value) ? "" : $"pick = opts.find(o => o.value === {J(value!)}) || null; ") +
                (string.IsNullOrWhiteSpace(label) ? "" : $"pick = pick || opts.find(o => (o.text||'').trim() === {J(label!)}) || null; ") +
                "if (!pick && opts.length === 1) pick = opts[0]; if (!pick) return 'NOOPTION'; el.value = pick.value; " +
                "el.dispatchEvent(new Event('input',{bubbles:true})); el.dispatchEvent(new Event('change',{bubbles:true})); " +
                "return 'OK:' + (pick.text||'').trim(); })()";
            var raw = UnwrapJsString(await EvalAsync(js).ConfigureAwait(true));
            if (raw == "NOTFOUND") throw new InvalidOperationException($"Element not found for selector: {selector}");
            if (raw == "NOOPTION") throw new InvalidOperationException($"No matching option in {selector}.");
            WriteStatusFile();
            return "selected " + raw[3..];
        }

        public async Task<SnapshotResult> DoSnapshotAsync(int maxNodes)
        {
            int max = Math.Clamp(maxNodes, 20, 1000);
            var js = "(function(MAX){var out=[];var els=document.querySelectorAll('a,button,input,select,textarea,[role=button],[role=link],[role=checkbox],[role=radio],[role=textbox],[role=switch],h1,h2,h3');" +
                "function path(el){var p=[];var n=el;while(n&&n.nodeType===1&&n!==document.body&&p.length<5){var s=n.tagName.toLowerCase();" +
                "if(n.id){s+='#'+n.id;p.unshift(s);break}var c=n,i=1;while((c=c.previousElementSibling)!=null){if(c.tagName===n.tagName)i++}" +
                "s+=':nth-of-type('+i+')';p.unshift(s);n=n.parentElement}return p.join('>')}" +
                "function nm(el){return ((el.getAttribute('aria-label')||el.innerText||el.value||el.getAttribute('placeholder')||el.getAttribute('title')||el.getAttribute('alt')||'')+'').trim().replace(/\\s+/g,' ').slice(0,80)}" +
                "for(var i=0;i<els.length&&out.length<MAX;i++){var el=els[i];var t=el.tagName.toLowerCase();var r=el.getAttribute('role')||'';" +
                "var extra='';if(t==='input'){extra=' type='+(el.getAttribute('type')||'text')}if(el.disabled)extra+=' [disabled]';" +
                "var h=t==='input'||t==='select'||t==='textarea';out.push('['+out.length+'] '+(r||t)+extra+' \"'+nm(el)+'\" <'+path(el)+'>')}" +
                "return JSON.stringify({nodes:out,truncated:els.length>MAX,total:els.length})})(" + max + ")";
            var raw = UnwrapJsString(await EvalAsync(js).ConfigureAwait(true));
            string text = ""; int total = 0; bool truncated = false;
            try
            {
                using var doc = JsonDocument.Parse(raw);
                total = doc.RootElement.GetProperty("total").GetInt32();
                truncated = doc.RootElement.GetProperty("truncated").GetBoolean();
                text = string.Join("\n", doc.RootElement.GetProperty("nodes").EnumerateArray().Select(n => n.GetString() ?? ""));
            }
            catch { text = raw; }
            if (text.Length > 20000) { text = text[..20000] + "\n...[truncated]"; truncated = true; }
            WriteStatusFile();
            return new SnapshotResult(true, text, total, truncated, "");
        }

        // ── Tab ops ──────────────────────────────────────────────────────────
        public async Task<List<TabInfo>> GetTabsAsync()
        {
            var list = new List<TabInfo>();
            for (int i = 0; i < _tabs.Count; i++)
            {
                list.Add(await TabInfoAsync(i).ConfigureAwait(true));
            }
            return list;
        }

        public async Task<TabInfo> OpenTabAsync(string? url, string waitUntil, int timeoutMs)
        {
            var tab = await AddTabAsync(url).ConfigureAwait(true);
            var view = tab.View;
            bool done = await WaitNavAsync(view, timeoutMs).ConfigureAwait(true);
            if (!done) throw new TimeoutException($"Tab navigation timed out after {timeoutMs}ms.");
            if (waitUntil == "domcontentloaded")
                await WaitReadyAsync(TimeSpan.FromMilliseconds(timeoutMs), "document.readyState === 'interactive' || document.readyState === 'complete'").ConfigureAwait(true);
            else if (waitUntil != "commit")
                await WaitReadyAsync(TimeSpan.FromMilliseconds(timeoutMs), "document.readyState === 'complete'").ConfigureAwait(true);
            await Task.Delay(200).ConfigureAwait(true);
            WriteStatusFile();
            var core = view.CoreWebView2;
            string ourl = core?.Source ?? "";
            if (string.IsNullOrEmpty(ourl) && core != null)
            {
                try { ourl = UnwrapJsString(await core.ExecuteScriptAsync("document.URL").ConfigureAwait(true)); } catch { }
            }
            return new TabInfo(_tabs.IndexOf(tab), ourl, core == null ? "" : await TitleOfAsync(core).ConfigureAwait(true), true);
        }

        public async Task<string> SwitchTabAsync(int index)
        {
            ActivateTab(index);
            var core = ActiveCore;
            return $"switched to tab {index}" + (core == null ? "" : $" ({core.Source})");
        }

        public async Task<string> CloseTabAsync(int? index)
        {
            int ix = index ?? _activeTab;
            if (ix < 0 || ix >= _tabs.Count) throw new InvalidOperationException($"No tab at index {ix} (have {_tabs.Count}).");
            if (_tabs.Count == 1)
            {
                bool ok = await ResetToBlankAsync().ConfigureAwait(true);
                return ok ? "cleared the only tab" : "no-op";
            }
            var view = _tabs[ix].View;
            try { Controls.Remove(view); } catch { }
            try { view.Dispose(); } catch { }
            _tabs.RemoveAt(ix);
            if (_activeTab >= _tabs.Count) _activeTab = _tabs.Count - 1;
            ActivateTab(_activeTab);
            return $"closed tab {ix} ({_tabs.Count} left)";
        }

public async Task<string> DoEvaluateAsync(string script)
        {
            var wrapped = script.TrimStart().StartsWith("(") ? script : "(" + script + ")";
            var result = await EvalAsync(wrapped).ConfigureAwait(true);
            WriteStatusFile();
            return result;
        }

        public async Task DoWaitForAsync(string state, int timeoutMs)
        {
            var t = TimeSpan.FromMilliseconds(timeoutMs);
            switch (state)
            {
                case "domcontentloaded":
                    await WaitReadyAsync(t, "document.readyState === 'interactive' || document.readyState === 'complete'");
                    break;
                case "networkidle":
                    await WaitReadyAsync(t, "document.readyState === 'complete'");
                    await Task.Delay(500);
                    break;
                default:
                    await WaitReadyAsync(t, "document.readyState === 'complete'");
                    break;
            }
            WriteStatusFile();
        }

public async Task<TabInfo> TabInfoAsync(int index)
        {
            var core = _tabs[index].View.CoreWebView2;
            string url = core?.Source ?? "";
            if (string.IsNullOrEmpty(url) && core != null)
            {
                try { url = UnwrapJsString(await core.ExecuteScriptAsync("document.URL").ConfigureAwait(true)); } catch { }
            }
            string title = core == null ? "" : await TitleOfAsync(core).ConfigureAwait(true);
            return new TabInfo(index, url, title, index == _activeTab);
        }

public void WriteStatusFile()
        {
            try
            {
                var payload = new
                {
                    isRunning = true,
                    bridgePort = BridgePort,
                    pid = Environment.ProcessId,
                    profilePath = _profileDir,
                    activePage = string.IsNullOrEmpty(GetSource()) ? null : new { url = GetSource() },
                    proxy = ProxyServer,
                    tabsCount = _tabs.Count,
                    lastActivity = DateTime.UtcNow.ToString("o")
                };
                File.WriteAllText(_statusFile, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public void StartBridge() => _server ??= new LoopbackBridgeServer(DispatchRequestAsync);
        public void StopBridge()
        {
            _server?.Dispose();
            _server = null;
            try
            {
                var payload = new
                {
                    isRunning = false,
                    bridgePort = 0,
                    pid = Environment.ProcessId,
                    profilePath = _profileDir,
                    lastActivity = DateTime.UtcNow.ToString("o")
                };
                File.WriteAllText(_statusFile, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        // ── Bridge op router ─────────────────────────────────────────────────
        private async Task<string> DispatchRequestAsync(string requestJson)
        {
            try
            {
                using var req = JsonDocument.Parse(requestJson);
                var root = req.RootElement;
                string op = root.TryGetProperty("op", out var opEl) ? opEl.GetString() ?? "" : "";
                var args = root.TryGetProperty("args", out var argEl) ? argEl : default;

                // Every WebView2 call MUST happen on the UI thread.
                if (InvokeRequired)
                {
                    return await InvokeOnUiAsync(op, args).ConfigureAwait(false);
                }
                return await RunOpAsync(op, args).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return BridgeResponses.Error(ex.Message);
            }
        }

        private async Task<string> InvokeOnUiAsync(string op, JsonElement args)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                BeginInvoke(new Action(async () =>
                {
                    try { tcs.SetResult(await RunOpAsync(op, args).ConfigureAwait(true)); }
                    catch (Exception ex) { tcs.SetResult(BridgeResponses.Error(ex.Message)); }
                }));
            }
            catch (Exception ex)
            {
                return BridgeResponses.Error("ui-invoke:" + ex.Message);
            }
            return await tcs.Task.ConfigureAwait(false);
        }

        private async Task<string> RunOpAsync(string op, JsonElement args)
        {
            try
            {
                switch (op)
                {
                    case "status":
                        return BridgeResponses.Ok(await BuildLaunchResultAsync().ConfigureAwait(true));

                    case "launch":
                    {
                        string? ps = GetStr(args, "proxyServer");
                        string? pu = GetStr(args, "proxyUsername");
                        string? pw = GetStr(args, "proxyPassword");
                        await EnsureCoreAsync(ps, pu, pw).ConfigureAwait(true);
                        WriteStatusFile();
                        return BridgeResponses.Ok(await BuildLaunchResultAsync().ConfigureAwait(true));
                    }

                    case "navigate":
                    {
                        string url = GetStr(args, "url") ?? "";
                        if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException("URL is required.");
                        string waitUntil = GetStr(args, "waitUntil") ?? "load";
                        int timeout = GetInt(args, "timeoutMs", 60000);
                        var r = await DoNavigateAsync(url, waitUntil, timeout).ConfigureAwait(true);
                        return BridgeResponses.Ok(r);
                    }

                    case "get_content":
                    {
                        string format = GetStr(args, "format") ?? "text";
                        string? selector = GetStr(args, "selector");
                        int maxChars = GetInt(args, "maxChars", 50000);
                        var r = await DoGetContentAsync(format, selector, maxChars).ConfigureAwait(true);
                        return BridgeResponses.Ok(r);
                    }

                    case "screenshot":
                    {
                        bool full = GetBool(args, "fullPage");
                        var r = await DoScreenshotAsync(full).ConfigureAwait(true);
                        return BridgeResponses.Ok(r);
                    }

                    case "click":
                    {
                        string selector = GetStr(args, "selector") ?? "";
                        string button = GetStr(args, "button") ?? "left";
                        if (string.IsNullOrWhiteSpace(selector)) throw new InvalidOperationException("Selector is required.");
                        await DoClickAsync(selector, button).ConfigureAwait(true);
                        return BridgeResponses.Ok(new ActionResult(true, GetSource(), await GetTitleAsync().ConfigureAwait(true) ?? "", "clicked"));
                    }

                    case "fill":
                    {
                        string selector = GetStr(args, "selector") ?? "";
                        string value = GetStr(args, "value") ?? "";
                        if (string.IsNullOrWhiteSpace(selector)) throw new InvalidOperationException("Selector is required.");
                        await DoFillAsync(selector, value).ConfigureAwait(true);
                        return BridgeResponses.Ok(new ActionResult(true, GetSource(), await GetTitleAsync().ConfigureAwait(true) ?? "", "filled"));
                    }

                    case "type":
                    {
                        string selector = GetStr(args, "selector") ?? "";
                        string text = GetStr(args, "text") ?? "";
                        int delay = GetInt(args, "delayMs", 50);
                        if (string.IsNullOrWhiteSpace(selector)) throw new InvalidOperationException("Selector is required.");
                        await DoTypeAsync(selector, text, delay).ConfigureAwait(true);
                        return BridgeResponses.Ok(new ActionResult(true, GetSource(), await GetTitleAsync().ConfigureAwait(true) ?? "", "typed"));
                    }

                    case "press_key":
                    {
                        string key = GetStr(args, "key") ?? "";
                        string? selector = GetStr(args, "selector");
                        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Key is required.");
                        await DoPressKeyAsync(key, selector).ConfigureAwait(true);
                        return BridgeResponses.Ok(new ActionResult(true, GetSource(), await GetTitleAsync().ConfigureAwait(true) ?? "", "pressed"));
                    }

                    case "evaluate":
                    {
                        string script = GetStr(args, "script") ?? "";
                        if (string.IsNullOrWhiteSpace(script)) throw new InvalidOperationException("Script is required.");
                        var raw = await DoEvaluateAsync(script).ConfigureAwait(true);
                        return BridgeResponses.Ok(new ActionResult(true, GetSource(), await GetTitleAsync().ConfigureAwait(true) ?? "", raw));
                    }

                    case "wait_for":
                    {
                        string state = GetStr(args, "state") ?? "networkidle";
                        int timeout = GetInt(args, "timeoutMs", 30000);
                        await DoWaitForAsync(state, timeout).ConfigureAwait(true);
                        return BridgeResponses.Ok(new WaitResult(true, GetSource(), await GetTitleAsync().ConfigureAwait(true) ?? "", ""));
                    }

case "get_tabs":
                        return BridgeResponses.Ok(await GetTabsAsync().ConfigureAwait(true));

                    case "open_tab":
                    {
                        string? url = GetStr(args, "url");
                        string waitUntil = GetStr(args, "waitUntil") ?? "load";
                        int timeout = GetInt(args, "timeoutMs", 60000);
                        var r = await OpenTabAsync(url, waitUntil, timeout).ConfigureAwait(true);
                        return BridgeResponses.Ok(r);
                    }

                    case "switch_tab":
                    {
                        int index = GetInt(args, "index", -1);
                        if (index < 0) throw new InvalidOperationException("Tab index is required.");
                        var msg = await SwitchTabAsync(index).ConfigureAwait(true);
                        return BridgeResponses.Ok(new ActionResult(true, GetSource(), await GetTitleAsync().ConfigureAwait(true) ?? "", msg));
                    }

                    case "close_tab":
                    {
                        int ix = GetInt(args, "index", -1);
                        var msg = await CloseTabAsync(ix < 0 ? null : ix).ConfigureAwait(true);
                        return BridgeResponses.Ok(new ActionResult(true, GetSource(), await GetTitleAsync().ConfigureAwait(true) ?? "", msg));
                    }

                    case "click_at":
                    {
                        int x = GetInt(args, "x", -1);
                        int y = GetInt(args, "y", -1);
                        string button = GetStr(args, "button") ?? "left";
                        int clicks = GetInt(args, "clicks", 1);
                        if (x < 0 || y < 0) throw new InvalidOperationException("x and y screenshot coordinates are required.");
                        var msg = await DoClickAtAsync(x, y, button, clicks).ConfigureAwait(true);
                        return BridgeResponses.Ok(new ActionResult(true, GetSource(), await GetTitleAsync().ConfigureAwait(true) ?? "", msg));
                    }

                    case "hover_at":
                    {
                        int x = GetInt(args, "x", -1);
                        int y = GetInt(args, "y", -1);
                        if (x < 0 || y < 0) throw new InvalidOperationException("x and y screenshot coordinates are required.");
                        var msg = await DoHoverAtAsync(x, y).ConfigureAwait(true);
                        return BridgeResponses.Ok(new ActionResult(true, GetSource(), await GetTitleAsync().ConfigureAwait(true) ?? "", msg));
                    }

                    case "scroll":
                    {
                        string direction = GetStr(args, "direction") ?? "down";
                        int pixels = GetInt(args, "pixels", 600);
                        int x = GetInt(args, "x", -1);
                        int y = GetInt(args, "y", -1);
                        string? selector = GetStr(args, "selector");
                        var msg = await DoScrollAsync(direction, pixels, x, y, selector).ConfigureAwait(true);
                        return BridgeResponses.Ok(new ActionResult(true, GetSource(), await GetTitleAsync().ConfigureAwait(true) ?? "", msg));
                    }

                    case "drag":
                    {
                        int x1 = GetInt(args, "x1", -1);
                        int y1 = GetInt(args, "y1", -1);
                        int x2 = GetInt(args, "x2", -1);
                        int y2 = GetInt(args, "y2", -1);
                        int steps = GetInt(args, "steps", 10);
                        if (x1 < 0 || y1 < 0 || x2 < 0 || y2 < 0) throw new InvalidOperationException("x1,y1,x2,y2 screenshot coordinates are required.");
                        var msg = await DoDragAsync(x1, y1, x2, y2, steps).ConfigureAwait(true);
                        return BridgeResponses.Ok(new ActionResult(true, GetSource(), await GetTitleAsync().ConfigureAwait(true) ?? "", msg));
                    }

                    case "go_back":
                    {
                        int timeout = GetInt(args, "timeoutMs", 15000);
                        var msg = await DoHistoryAsync("back", timeout).ConfigureAwait(true);
                        return BridgeResponses.Ok(new ActionResult(true, GetSource(), await GetTitleAsync().ConfigureAwait(true) ?? "", msg));
                    }

                    case "go_forward":
                    {
                        int timeout = GetInt(args, "timeoutMs", 15000);
                        var msg = await DoHistoryAsync("forward", timeout).ConfigureAwait(true);
                        return BridgeResponses.Ok(new ActionResult(true, GetSource(), await GetTitleAsync().ConfigureAwait(true) ?? "", msg));
                    }

                    case "reload":
                    {
                        int timeout = GetInt(args, "timeoutMs", 30000);
                        var msg = await DoHistoryAsync("reload", timeout).ConfigureAwait(true);
                        return BridgeResponses.Ok(new ActionResult(true, GetSource(), await GetTitleAsync().ConfigureAwait(true) ?? "", msg));
                    }

                    case "set_viewport":
                    {
                        int width = GetInt(args, "width", 0);
                        int height = GetInt(args, "height", 0);
                        double scale = GetDbl(args, "scale", 0);
                        var r = await DoViewportAsync(width, height, scale).ConfigureAwait(true);
                        return BridgeResponses.Ok(r);
                    }

                    case "console":
                    {
                        var r = await DoConsoleAsync().ConfigureAwait(true);
                        return BridgeResponses.Ok(r);
                    }

                    case "upload":
                    {
                        string selector = GetStr(args, "selector") ?? "";
                        string filePath = GetStr(args, "filePath") ?? "";
                        if (string.IsNullOrWhiteSpace(selector)) throw new InvalidOperationException("Selector is required.");
                        var msg = await DoUploadAsync(selector, filePath).ConfigureAwait(true);
                        return BridgeResponses.Ok(new ActionResult(true, GetSource(), await GetTitleAsync().ConfigureAwait(true) ?? "", msg));
                    }

                    case "select_option":
                    {
                        string selector = GetStr(args, "selector") ?? "";
                        string? value = GetStr(args, "value");
                        string? label = GetStr(args, "label");
                        if (string.IsNullOrWhiteSpace(selector)) throw new InvalidOperationException("Selector is required.");
                        var msg = await DoSelectAsync(selector, value, label).ConfigureAwait(true);
                        return BridgeResponses.Ok(new ActionResult(true, GetSource(), await GetTitleAsync().ConfigureAwait(true) ?? "", msg));
                    }

                    case "snapshot":
                    {
                        int maxNodes = GetInt(args, "maxNodes", 300);
                        var r = await DoSnapshotAsync(maxNodes).ConfigureAwait(true);
                        return BridgeResponses.Ok(r);
                    }

                    case "set_proxy":
                    {
                        string? server = GetStr(args, "server");
                        string? user = GetStr(args, "username");
                        string? pass = GetStr(args, "password");
                        bool changed = RestartRequired(server, user, pass);
                        await ApplyProxyAsync(server, user, pass).ConfigureAwait(true);
                        WriteStatusFile();
                        return BridgeResponses.Ok(new SetProxyResult(true, changed, GetSource()));
                    }

                    case "close":
                        await ControlCloseAsync().ConfigureAwait(true);
                        return BridgeResponses.Ok(await BuildLaunchResultAsync().ConfigureAwait(true));

                    default:
                        throw new InvalidOperationException($"Unknown browser op: {op}");
                }
            }
            catch (Exception ex)
            {
                return BridgeResponses.Error(ex.Message);
            }
        }

        private bool RestartRequired(string? server, string? user, string? pass)
        {
            bool sameServer = string.Equals(ProxyServer ?? "", server ?? "", StringComparison.OrdinalIgnoreCase);
            bool sameUser = string.Equals(ProxyUsername ?? "", user ?? "", StringComparison.OrdinalIgnoreCase);
            bool samePass = string.Equals(ProxyPassword ?? "", pass ?? "", StringComparison.OrdinalIgnoreCase);
            return !(sameServer && sameUser && samePass);
        }

private async Task<LaunchResult> BuildLaunchResultAsync()
        {
            var title = await GetTitleAsync().ConfigureAwait(true) ?? "";
            var active = GetSource();
            return new LaunchResult(true, _profileDir, active, title, Math.Max(_tabs.Count, 1), ProxyServer, "");
        }

private async Task<bool> ResetToBlankAsync()
        {
            if (_webView.CoreWebView2 == null) return false;
            try
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                EventHandler<CoreWebView2NavigationCompletedEventArgs>? h = null;
                h = (_, _) => { tcs.TrySetResult(true); try { _webView.NavigationCompleted -= h; } catch { } };
                _webView.NavigationCompleted += h;
                _webView.CoreWebView2.Navigate("about:blank");
                await Task.WhenAny(tcs.Task, Task.Delay(10000)).ConfigureAwait(true);
                return true;
            }
            catch { return false; }
        }

        private async Task ControlCloseAsync()
        {
            if (_webView.CoreWebView2 == null) return;
            try
            {
                await ResetToBlankAsync().ConfigureAwait(true);
            }
            catch { }
        }

        private static string? GetStr(JsonElement args, string name)
        {
            if (args.ValueKind != JsonValueKind.Object) return null;
            return args.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetString() : null;
        }

        private static int GetInt(JsonElement args, string name, int dflt)
        {
            if (args.ValueKind != JsonValueKind.Object) return dflt;
            return args.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : dflt;
        }

private static bool GetBool(JsonElement args, string name)
        {
            if (args.ValueKind != JsonValueKind.Object) return false;
            return args.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
        }

        private static double GetDbl(JsonElement args, string name, double dflt)
        {
            if (args.ValueKind != JsonValueKind.Object) return dflt;
            return args.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : dflt;
        }

internal static string UnwrapJsString(string raw)
        {
            try { return JsonSerializer.Deserialize<string>(raw) ?? ""; }
            catch { return raw.Trim('"'); }
        }
    }
    // ============================================================
    //  CDP KEY MAP
    // ============================================================

    internal static class KeyMap
    {
        public readonly record struct Entry(string Key, string Code, int Vk, bool IsChar = false, string Text = "");

        private static readonly Dictionary<string, Entry> Map = new(StringComparer.OrdinalIgnoreCase)
        {
            ["enter"] = new("Enter", "Enter", 13),
            ["return"] = new("Enter", "Enter", 13),
            ["tab"] = new("Tab", "Tab", 9),
            ["escape"] = new("Escape", "Escape", 27),
            ["esc"] = new("Escape", "Escape", 27),
            ["backspace"] = new("Backspace", "Backspace", 8),
            ["delete"] = new("Delete", "Delete", 46),
            ["space"] = new(" ", "Space", 32),
            ["arrowup"] = new("ArrowUp", "ArrowUp", 38),
            ["up"] = new("ArrowUp", "ArrowUp", 38),
            ["arrowdown"] = new("ArrowDown", "ArrowDown", 40),
            ["down"] = new("ArrowDown", "ArrowDown", 40),
            ["arrowleft"] = new("ArrowLeft", "ArrowLeft", 37),
            ["left"] = new("ArrowLeft", "ArrowLeft", 37),
            ["arrowright"] = new("ArrowRight", "ArrowRight", 39),
            ["right"] = new("ArrowRight", "ArrowRight", 39),
            ["home"] = new("Home", "Home", 36),
            ["end"] = new("End", "End", 35),
            ["pageup"] = new("PageUp", "PageUp", 33),
            ["pagedown"] = new("PageDown", "PageDown", 34),
            ["."] = new(".", "Period", 190),
            [","] = new(",", "Comma", 188),
            ["/"] = new("/", "Slash", 191),
            [";"] = new(";", "Semicolon", 186),
            ["'"] = new("'", "Quote", 222),
            ["["] = new("[", "BracketLeft", 219),
            ["]"] = new("]", "BracketRight", 221),
            ["-"] = new("-", "Minus", 189),
            ["="] = new("=", "Equal", 187),
        };

        public static Entry Get(string key)
        {
            if (Map.TryGetValue(key, out var mapped)) return mapped;

            // F1..F12
            if (key.Length > 1 && key[0] == 'F' && int.TryParse(key[1..], out var f) && f is >= 1 and <= 12)
                return new Entry("F" + f, "F" + f, 111 + f);

            // single character
            if (key.Length == 1)
            {
                if (char.IsDigit(key[0]))
                    return new Entry(key, "Digit" + key[0], 48 + (key[0] - '0'), IsChar: true, Text: key);
                if (char.IsLetter(key[0]))
                {
                    var upper = char.ToUpperInvariant(key[0]);
                    return new Entry(key, "Key" + upper, upper, IsChar: true, Text: key);
                }
                return new Entry(key, "", 0, IsChar: true, Text: key);
            }

            throw new InvalidOperationException($"Unsupported key: {key}");
        }
    }
}
