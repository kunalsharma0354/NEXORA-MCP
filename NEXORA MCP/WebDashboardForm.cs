using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace NEXORA_MCP
{
    /// <summary>
    /// Dashboard rendered as a NEXT.JS UI inside WebView2 (same dark theme, Linear/Vercel style).
    ///
    /// Architecture:
    ///   - The built Next.js static export lives in "&lt;exe&gt;\webui\" and is served to the
    ///     WebView via SetVirtualHostNameToFolderMapping → https://nexora.ui/index.html.
    ///   - C# pushes ALL live data (server state, stats, activity log, models, session,
    ///     browser state) into the page as JSON on a 2.5s poll → { type:'nx:data', payload }.
    ///   - The page posts commands back → { type:'nx:cmd', cmd } :
    ///     ui.ready, drag, minimize, close, logout, browser.toggle, model.select { key }.
    ///   - The embedded browser (EmbeddedBrowserPanel) lives BENEATH the WebView panel in
    ///     z-order, exactly like the old WinForms widgets overlay did — it stays rendering
    ///     so MCP screenshots keep working; BROWSER toggles z-order.
    ///
    /// If the webui build is missing the old DashboardForm is used instead — see CreateDashboard.
    /// </summary>
    public sealed class WebDashboardForm : Form
    {
        private WebView2 _webView = null!;
        private EmbeddedBrowserPanel? _browserPanel;
        private Label _errorLabel = null!;
        private System.Windows.Forms.Timer _refreshTimer = null!;

        // Browser mode chrome — a 1:1 WinForms copy of the dashboard top bar
        // (web/components/TopBar.jsx) shown ABOVE the embedded browser.
        private Panel _browserWrap = null!;
        private Panel _chrome = null!;
        private ChromeBrandDot _chromeDot = null!;
        private ChromeLabel _lblChromeBrand = null!;
        private ChromeLabel _lblChromeSub = null!;
        private ChromeStatusPill _chromePill = null!;
        private ChromeModelTrigger _chromeModel = null!;
        private ChromeGhostButton _btnChromeDash = null!;
        private ChromeGhostButton _btnChromeLogout = null!;
        private ChromeDivider _chromeDivider = null!;
        private ChromeWindowButton _btnChromeMin = null!;
        private ChromeWindowButton _btnChromeClose = null!;
        private ChromeModelDropdown? _modelDrop;
        private string _chromeState = "connecting";

        private bool _webReady;
        private bool _browserVisible;

        // ─── Model data (parity with DashboardForm.AiModel) ──────────────────
        private sealed class AiModel
        {
            public string Key { get; set; } = "";
            public string Name { get; set; } = "";
            public int MaxTokens { get; set; } = 16384;
            public int Order { get; set; }
        }

        private readonly List<AiModel> _models = new();
        private AiModel? _selectedModel;
        private bool _modelsLoaded;

        private static readonly string ActivityLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nexora", "mcp_activity.json");

        private static readonly string StatusFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nexora", "browser_status.json");

        private static string WebUiRoot => Path.Combine(AppContext.BaseDirectory, "webui");

        public WebDashboardForm()
        {
            InitializeComponent();
            BuildUi();

            Load += async (_, _) =>
            {
                FitToScreen();
                EnsureBrowserPanel();
                await LoadModelsAsync();
                await InitWebAsync();
            };

            // Push data on the same 2.5s cadence the old WinForms dashboard used.
            _refreshTimer = new System.Windows.Forms.Timer { Interval = 2500 };
            _refreshTimer.Tick += (_, _) => PushData();
            _refreshTimer.Start();
        }

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTION / LAYOUT
        // ═════════════════════════════════════════════════════════════════

        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = UITheme.Back;
            ClientSize = new Size(1080, 720);
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "WebDashboardForm";
            StartPosition = FormStartPosition.Manual;
            Text = "NEXORA — Dashboard";
            DoubleBuffered = true;
            ResumeLayout(false);
        }

        private void BuildUi()
        {
            // Error overlay (shown only if WebView2 init fails).
            _errorLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = UITheme.Back,
                ForeColor = UITheme.TextSecondary,
                Font = new Font("Segoe UI", 10F),
                Text = "Loading dashboard…",
                Visible = false
            };

            // Embedded browser WRAPPER: a 1px "#2A2A2A"-colored padding ring gives the
            // browser proper border lines on all 4 sides, so it reads as a browser
            // living inside the NEXORA app, not a fullscreen takeover.
            _browserWrap = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(42, 42, 42),
                Padding = new Padding(1, 1, 1, 1),
                Visible = true // always true so WebView2 keeps compositing for the MCP agent
            };
            _browserPanel = new EmbeddedBrowserPanel
            {
                Dock = DockStyle.Fill,
                Visible = true
            };
            _browserWrap.Controls.Add(_browserPanel);
            Controls.Add(_browserWrap);
            _browserWrap.SendToBack();

            // Dashboard WebView on top (browser mode swaps z-order instead of
            // visibility, so BOTH webviews keep rendering).
            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                AllowExternalDrop = false
            };
            Controls.Add(_webView);
            Controls.Add(_errorLabel);
            _webView.BringToFront();

            BuildBrowserChrome();
            Resize += (_, _) => PositionChrome();

        }

        /// <summary>Factory used by the login form: prefer the Next.js UI, fall back to
        /// the legacy WinForms dashboard when the webui build is missing.</summary>
        public static Form CreateDashboard()
        {
            if (File.Exists(Path.Combine(WebUiRoot, "index.html")))
            {
                try { return new WebDashboardForm(); }
                catch { /* fall through to legacy */ }
            }
            return new DashboardForm();
        }

        /// <summary>Clamp the whole window into the working area and center it. Screen.WorkingArea
        /// and Bounds are read in the SAME WinForms coordinate space, so this needs no manual
        /// DPI conversion and holds at 100%, 125% and 150% display scaling.</summary>
        private void FitToScreen()
        {
            try
            {
                var wa = Screen.PrimaryScreen.WorkingArea;
                var b = Bounds;

                if (b.Width > wa.Width - 24 || b.Height > wa.Height - 24)
                {
                    double f = Math.Min((wa.Width - 24.0) / b.Width, (wa.Height - 24.0) / b.Height);
                    Size = new Size((int)Math.Round(b.Width * f), (int)Math.Round(b.Height * f));
                }

                var waFinal = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(
                    waFinal.X + Math.Max(0, (waFinal.Width - Width) / 2),
                    waFinal.Y + Math.Max(0, (waFinal.Height - Height) / 2));
            }
            catch
            {
                StartPosition = FormStartPosition.CenterScreen;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  WEBVIEW HOSTING
        // ═════════════════════════════════════════════════════════════════

        private async Task InitWebAsync()
        {
            try
            {
                var userData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Nexora", "WebView2Dui");

                var env = await CoreWebView2Environment.CreateAsync(null, userData);

                try { _webView.DefaultBackgroundColor = Color.FromArgb(10, 10, 10); } catch { /* older sdk */ }

                await _webView.EnsureCoreWebView2Async(env);
                var core = _webView.CoreWebView2;

                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.IsZoomControlEnabled = false;

                core.SetVirtualHostNameToFolderMapping(
                    "nexora.ui", WebUiRoot, CoreWebView2HostResourceAccessKind.Allow);
                core.WebMessageReceived += OnWebMessageReceived;

                core.Navigate("https://nexora.ui/index.html");
                _webReady = true;
                PushData();
            }
            catch (Exception ex)
            {
                _webView.Visible = false;
                _errorLabel.Visible = true;
                _errorLabel.Text = $"Dashboard web UI failed to load.\n\n{ex.Message}";
                _refreshTimer.Stop();
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  DATA PUSH (C# → JS) — mirrors old RefreshActivityLog exactly
        // ═════════════════════════════════════════════════════════════════

        private sealed class ActivityEntry
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

        private static List<ActivityEntry> ReadActivity()
        {
            try
            {
                if (!File.Exists(ActivityLogPath)) return new List<ActivityEntry>();
                var json = File.ReadAllText(ActivityLogPath);
                var entries = JsonSerializer.Deserialize<List<ActivityEntry>>(json);
                return entries ?? new List<ActivityEntry>();
            }
            catch
            {
                return new List<ActivityEntry>();
            }
        }

        private static bool BrowserRunning()
        {
            try
            {
                if (!File.Exists(StatusFilePath)) return false;
                using var doc = JsonDocument.Parse(File.ReadAllText(StatusFilePath));
                if (doc.RootElement.TryGetProperty("isRunning", out var run))
                    return run.ValueKind == JsonValueKind.True;
            }
            catch { }
            return false;
        }

        private void PushData()
        {
            if (!_webReady || _webView.CoreWebView2 == null || IsDisposed) return;

            var entries = ReadActivity();
            var toolEntries = entries.Where(e => e.Type == "tool_call").ToList();
            var pendingEntry = toolEntries.FirstOrDefault(e => e.Status == "pending");

            bool active = TokenStore.IsLoggedIn;
            bool processing = pendingEntry != null;

            string state = processing ? "processing" : active ? "active" : "inactive";
            UpdateChromeState(state);
            string detail;
            if (toolEntries.Count == 0) detail = "No tool calls yet — server is idle and waiting.";
            else if (processing) detail = $"Processing \"{Truncate(pendingEntry!.Input ?? "", 70)}\"...";
            else detail = $"Last call: {FormatTime(toolEntries[^1].Timestamp)} · Server is idle";

            int latencyMs = EstimateLatency(toolEntries);
            var recent = toolEntries.TakeLast(15).Reverse().ToList();

            bool browserRunning = BrowserRunning();

            var payload = new
            {
                state,
                detail,
                stats = new
                {
                    totalCalls = toolEntries.Count,
                    successRate = toolEntries.Count > 0
                        ? (int)((double)toolEntries.Count(e => e.Success && e.Status != "pending") / toolEntries.Count * 100)
                        : 0,
                    latencyMs
                },
                models = _models.Select(m => new { key = m.Key, name = m.Name, maxTokens = m.MaxTokens }).ToArray(),
                selectedModelKey = _selectedModel?.Key ?? "",
                activity = recent.Select(e => new
                {
                    ts = e.Timestamp,
                    tool = e.Tool,
                    status = e.Status ?? "done",
                    success = e.Success,
                    input = e.Input ?? "",
                    output = e.Output ?? "",
                    model = e.Model ?? "",
                    error = e.Error
                }).ToArray(),
                session = new
                {
                    username = TokenStore.Username,
                    hwid = HwidShort()
                },
                browser = new { running = browserRunning }
            };

            var json = JsonSerializer.Serialize(new { type = "nx:data", payload });
            _webView.CoreWebView2.PostWebMessageAsJson(json);
        }

        private static string HwidShort()
        {
            try
            {
                var hwid = HwidHelper.GetOrCreateDeviceId();
                if (hwid.Length > 8) return hwid[^8..].ToUpperInvariant();
            }
            catch { }
            return "";
        }

        // ═════════════════════════════════════════════════════════════════
        //  SKILLS OVERLAY — registry listing pushed on demand from the topbar
        // ═════════════════════════════════════════════════════════════════

        private void SendSkills()
        {
            if (!_webReady || _webView.CoreWebView2 == null || IsDisposed) return;
            try
            {
                var snapshot = SkillsRegistry.Instance.Current;
                if (snapshot.Skills.Count == 0 && snapshot.Issues.Count == 0)
                {
                    SkillsRegistry.Instance.Refresh();
                    snapshot = SkillsRegistry.Instance.Current;
                }

                var skills = snapshot.Skills
                    .OrderBy(s => s.Category, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(s => new
                    {
                        id = s.Id,
                        name = s.Name,
                        category = s.Category,
                        description = s.Description,
                        purpose = s.Purpose,
                        status = s.EffectiveStatus,
                        version = s.Version,
                        trust = s.Trust,
                        tags = s.Tags,
                        dependencies = s.Dependencies,
                        requiresTools = s.RequiresTools,
                        hasInstructions = s.HasInstructions
                    })
                    .ToArray();

                var payload = new { count = skills.Length, issueCount = snapshot.Issues.Count, skills };
                var json = JsonSerializer.Serialize(new { type = "nx:skills", payload });
                _webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch
            {
                // Never crash the dashboard over a data push.
            }
        }

        private void SendSkillInstructions(string id)
        {
            if (!_webReady || _webView.CoreWebView2 == null || IsDisposed) return;
            try
            {
                var snapshot = SkillsRegistry.Instance.Current;
                var skill = SkillsRegistry.Find(snapshot, id);

                object payload;
                if (skill == null)
                {
                    payload = new { id, ok = false, name = "", instructions = "" };
                }
                else
                {
                    bool ok = skill.HasInstructions && !skill.HasError && !skill.InCycle;
                    payload = new { id = skill.Id, ok, name = skill.Name, instructions = skill.Instructions };
                }

                var json = JsonSerializer.Serialize(new { type = "nx:skill-instructions", payload });
                _webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch
            {
                // Never crash the dashboard over a data push.
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  COMMANDS (JS → C#)
        // ═════════════════════════════════════════════════════════════════

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                var root = doc.RootElement;
                if (!root.TryGetProperty("cmd", out var cmdEl)) return;
                var cmd = cmdEl.GetString() ?? "";

                switch (cmd)
                {
                    case "ui.ready":
                        PushData();
                        break;

                    case "drag":
                        ReleaseCapture();
                        SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
                        break;

                    case "minimize":
                        WindowState = FormWindowState.Minimized;
                        break;

                    case "close":
                        Close();
                        break;

                    case "logout":
                        Logout();
                        break;

                    case "browser.toggle":
                        ToggleBrowser();
                        break;

                    case "skills.list":
                        SendSkills();
                        break;

                    case "skills.load":
                        if (root.TryGetProperty("id", out var skillIdEl))
                            SendSkillInstructions(skillIdEl.GetString() ?? "");
                        break;

                    case "model.select":
                        if (root.TryGetProperty("key", out var keyEl))
                        {
                            var key = keyEl.GetString() ?? "";
                            var model = _models.FirstOrDefault(m => m.Key == key);
                            if (model != null) _selectedModel = model;
                            PushData();
                        }
                        break;
                }
            }
            catch
            {
                // Malformed message — ignore; the 2.5s poll keeps everything fresh.
            }
        }

        private void Logout()
        {
            TokenStore.ClearSession();
            var login = new Form1();
            Hide();
            login.FormClosed += (_, _) => Close();
            login.Show();
        }

        // ── Embedded browser toggle (parity with DashboardForm) ────────────
        private void ToggleBrowser()
        {
            EnsureBrowserPanel();
            _browserVisible = true;
            _chrome.Visible = true;
            UpdateChromeModelText();
            PositionChrome();
            _chrome.BringToFront();
            _browserWrap.BringToFront();
        }

        private void ShowDashboard()
        {
            _browserVisible = false;
            if (_modelDrop != null)
            {
                _modelDrop.Close();
                _modelDrop = null;
            }
            _chrome.Visible = false;
            _webView.Visible = true;
            _webView.BringToFront();
        }

        // ESC while the browser is open returns to the dashboard. Ctrl+B toggles the browser.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.B))
            {
                ToggleBrowser();
                return true;
            }
            if (keyData == Keys.Escape && _browserVisible)
            {
                ShowDashboard();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void EnsureBrowserPanel()
        {
            _browserPanel = _browserPanel ?? throw new InvalidOperationException("browser not built");
            _browserPanel.CreateControl();
            // Init the WebView2 core + bridge eagerly so MCP agent can drive the
            // browser even before the user toggles it into view.
            _ = _browserPanel.EnsureInitializedAsync();
        }

        // ═════════════════════════════════════════════════════════════════
        //  BROWSER-MODE CHROME — persistent NEXORA header above the browser
        // ═════════════════════════════════════════════════════════════════

        private void BuildBrowserChrome()
        {
            // .topbar — 56px, #0E0E0E, 1px #2A2A2A bottom border, drag anywhere
            // that is not an interactive control (same rule as the HTML topbar).
            _chrome = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)Math.Round(56 * (DeviceDpi / 96f)),
                BackColor = ChromePalette.TopBarBg,
                Visible = false
            };
            _chrome.Paint += (_, e) =>
            {
                using var pen = new Pen(ChromePalette.Border);
                e.Graphics.DrawLine(pen, 0, _chrome.Height - 1, _chrome.Width, _chrome.Height - 1);
            };
            _chrome.Resize += (_, _) => PositionChrome();
            HandleCreated += (_, _) => ApplyChromeScale();

            // .brand — dot + NEXORA + MCP DASHBOARD (mono, tracked).
            _chromeDot = new ChromeBrandDot();
            _chromeDot.SetState(_chromeState);

            _lblChromeBrand = new ChromeLabel
            {
                Text = "NEXORA",
                Font = ChromeFonts.Brand,
                ForeColor = ChromePalette.Text,
                Tracking = 0.96f
            };
            _lblChromeBrand.AutoSizeToText();

            _lblChromeSub = new ChromeLabel
            {
                Text = "MCP DASHBOARD",
                Font = ChromeFonts.BrandSub,
                ForeColor = ChromePalette.Muted,
                Tracking = 1.8f
            };
            _lblChromeSub.AutoSizeToText();

            // .status-pill — live MCP state with breathe + ripple.
            _chromePill = new ChromeStatusPill();
            _chromePill.SetState(_chromeState);
            _chromePill.SizeChanged += (_, _) => PositionChrome();

            // .selector-trigger — 250x34 model picker, wired to the same model
            // list the dashboard uses.
            _chromeModel = new ChromeModelTrigger();
            _chromeModel.Clicked += (_, _) => ToggleModelDropdown();

            // .ghost-btn — DASHBOARD (back) + LOGOUT.
            _btnChromeDash = new ChromeGhostButton("DASHBOARD");
            _btnChromeDash.Clicked += (_, _) => ShowDashboard();

            _btnChromeLogout = new ChromeGhostButton("LOGOUT");
            _btnChromeLogout.Clicked += (_, _) => Logout();

            _chromeDivider = new ChromeDivider();

            _btnChromeMin = new ChromeWindowButton(ChromeWinKind.Minimize);
            _btnChromeMin.Clicked += (_, _) => WindowState = FormWindowState.Minimized;

            _btnChromeClose = new ChromeWindowButton(ChromeWinKind.Close);
            _btnChromeClose.Clicked += (_, _) => Close();

            _chrome.Controls.AddRange(new Control[]
            {
                _chromeDot, _lblChromeBrand, _lblChromeSub, _chromePill, _chromeModel,
                _btnChromeDash, _btnChromeLogout, _chromeDivider, _btnChromeMin, _btnChromeClose
            });

            // HTML rule: mousedown anywhere in .topbar drags the window unless
            // the target is marked .nx-nodrag (buttons / selector / win buttons).
            AttachDrag(_chrome);
            AttachDrag(_chromeDot);
            AttachDrag(_lblChromeBrand);
            AttachDrag(_lblChromeSub);
            AttachDrag(_chromePill);

            Controls.Add(_chrome);
            UpdateChromeModelText();
        }

        private void AttachDrag(Control target)
        {
            target.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                ReleaseCapture();
                SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
            };
        }

        /// <summary>
        /// The dashboard header is HTML: its 56px are DIPs, so at 125% display
        /// scaling it is 70 physical pixels tall. The WinForms copy must follow
        /// the same rule or it looks visibly shorter/smaller than the dashboard.
        /// </summary>
        private void ApplyChromeScale()
        {
            if (_chrome == null) return;
            int target = (int)Math.Round(56 * (DeviceDpi / 96f));
            if (_chrome.Height != target) _chrome.Height = target;
            PositionChrome();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            ApplyChromeScale();
        }

        /// <summary>
        /// Lays the chrome out exactly like .topbar-inner: 24px left padding,
        /// 16px right padding, 10px gaps (12px around the 1x20 divider),
        /// everything vertically centred inside 56px (× DPI scale).
        /// </summary>
        private void PositionChrome()
        {
            if (_chrome is not { Width: > 0 } || _chromeDot == null) return;

            float k = _chrome.Height / 56f;
            int padL = (int)Math.Round(24 * k);
            int padR = (int)Math.Round(16 * k);
            int gap = (int)Math.Round(10 * k);
            int gapDiv = (int)Math.Round(12 * k);

            int Mid(int h) => (int)Math.Round((_chrome.Height - h) / 2f);

            // Left: brand block.
            _chromeDot.Size = new Size((int)Math.Round(9 * k), (int)Math.Round(9 * k));
            _chromeDot.Location = new Point(padL, Mid(_chromeDot.Height));

            int x = _chromeDot.Right + gap;
            _lblChromeBrand.Location = new Point(x, Mid(_lblChromeBrand.Height));

            x = _lblChromeBrand.Right + gap;
            _lblChromeSub.Location = new Point(x, Mid(_lblChromeSub.Height));

            // Right: pill, model selector, buttons, divider, window buttons.
            _chromePill.ApplyScale(k);
            _chromeModel.Size = new Size((int)Math.Round(250 * k), (int)Math.Round(34 * k));
            _btnChromeDash.MeasureFor(k);
            _btnChromeLogout.MeasureFor(k);
            _chromeDivider.Size = new Size(Math.Max(1, (int)Math.Round(1 * k)), (int)Math.Round(20 * k));
            _btnChromeMin.Size = new Size((int)Math.Round(30 * k), (int)Math.Round(26 * k));
            _btnChromeClose.Size = new Size((int)Math.Round(30 * k), (int)Math.Round(26 * k));

            int right = _chrome.Width - padR;

            right -= _btnChromeClose.Width;
            _btnChromeClose.Location = new Point(right, Mid(_btnChromeClose.Height));

            right -= gap + _btnChromeMin.Width;
            _btnChromeMin.Location = new Point(right, Mid(_btnChromeMin.Height));

            right -= gapDiv + _chromeDivider.Width;
            _chromeDivider.Location = new Point(right, Mid(_chromeDivider.Height));

            right -= gapDiv + _btnChromeLogout.Width;
            _btnChromeLogout.Location = new Point(right, Mid(_btnChromeLogout.Height));

            right -= gap + _btnChromeDash.Width;
            _btnChromeDash.Location = new Point(right, Mid(_btnChromeDash.Height));

            right -= gap + _chromeModel.Width;
            _chromeModel.Location = new Point(right, Mid(_chromeModel.Height));

            right -= gap + _chromePill.Width;
            _chromePill.Location = new Point(right, Mid(_chromePill.Height));
        }

        /// <summary>Keeps the pill, brand dot and selector text in sync with state.</summary>
        private void UpdateChromeState(string state)
        {
            _chromeState = state;
            _chromePill?.SetState(state);
            _chromeDot?.SetState(state);
            UpdateChromeModelText();
        }

        private void UpdateChromeModelText()
        {
            if (_chromeModel == null) return;
            _chromeModel.DisplayText = _selectedModel?.Name ?? "Select model…";
        }

        /// <summary>.selector-drop: borderless popup anchored under the trigger.</summary>
        private void ToggleModelDropdown()
        {
            if (_modelDrop != null)
            {
                _modelDrop.Close();
                return;
            }
            if (_models.Count == 0 || _chrome == null) return;

            var drop = new ChromeModelDropdown(
                _models.Select(m => (m.Key, m.Name)),
                _selectedModel?.Key ?? "");

            drop.ModelPicked += key =>
            {
                var model = _models.FirstOrDefault(m => m.Key == key);
                if (model != null) _selectedModel = model;
            };

            drop.FormClosed += (_, _) =>
            {
                _modelDrop = null;
                _chromeModel.DropdownOpen = false;
                PushData();
            };

            float k = _chrome.Height / 56f;
            var anchor = _chrome.PointToScreen(new Point(_chromeModel.Left, _chromeModel.Bottom));
            var wa = Screen.FromPoint(anchor).WorkingArea;

            int x = anchor.X + _chromeModel.Width - drop.Width;
            x = Math.Clamp(x, wa.Left, Math.Max(wa.Left, wa.Right - drop.Width));
            int y = anchor.Y + (int)Math.Round(6 * k);
            y = Math.Min(y, Math.Max(wa.Top, wa.Bottom - drop.Height));

            drop.Location = new Point(x, y);
            _chromeModel.DropdownOpen = true;
            _modelDrop = drop;
            drop.Show(this);
        }

        // ═════════════════════════════════════════════════════════════════
        //  MODELS (parity with DashboardForm.LoadModelsAsync)
        // ═════════════════════════════════════════════════════════════════

        private async Task LoadModelsAsync()
        {
            if (_modelsLoaded || string.IsNullOrWhiteSpace(TokenStore.AccessToken)) return;

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenStore.AccessToken);
                http.DefaultRequestHeaders.Add("Accept", "application/json");

                var resp = await http.GetAsync($"{TokenStore.BaseUrl}/api/ai-models");
                var raw = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode) { AddDefaultModel(); return; }

                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                if (root.TryGetProperty("models", out var modelsEl) && modelsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var m in modelsEl.EnumerateArray())
                    {
                        var model = new AiModel
                        {
                            Key = m.TryGetProperty("key", out var key) ? key.GetString() ?? "" : "",
                            Name = m.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                            MaxTokens = m.TryGetProperty("maxTokens", out var mt) ? mt.GetInt32() : 16384,
                            Order = m.TryGetProperty("order", out var ord) ? ord.GetInt32() : 0
                        };
                        var enabled = m.TryGetProperty("enabled", out var en) && en.GetBoolean();
                        if (enabled && !string.IsNullOrEmpty(model.Key))
                            _models.Add(model);
                    }
                }

                _models.Sort((a, b) => a.Order.CompareTo(b.Order));
                if (_models.Count > 0) _selectedModel = _models[0];
                _modelsLoaded = true;
                PushData();
            }
            catch { AddDefaultModel(); }
        }

        private void AddDefaultModel()
        {
            _models.Add(new AiModel
            {
                Key = "nvidia/nemotron-3-ultra-550b-a55b",
                Name = "Nemotron-3 Ultra 550B",
                MaxTokens = 16384,
                Order = 0
            });
            _selectedModel = _models[0];
            _modelsLoaded = true;
            BeginInvoke(PushData);
        }

        // ═════════════════════════════════════════════════════════════════
        //  HELPERS (parity with DashboardForm)
        // ═════════════════════════════════════════════════════════════════

        private static int EstimateLatency(List<ActivityEntry> entries)
        {
            var recent = entries.TakeLast(5).ToList();
            if (recent.Count == 0) return 0;
            int sum = 0;
            foreach (var e in recent)
                sum += Math.Min(2000, (e.Output?.Length ?? 0) * 3);
            return sum / recent.Count;
        }

        private static string FormatTime(string? iso)
        {
            if (string.IsNullOrEmpty(iso)) return "—";
            if (DateTimeOffset.TryParse(iso, out var dt))
                return dt.ToLocalTime().ToString("hh:mm:ss tt");
            return iso;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s[..max] + "...";
        }

        // ─── Public API (parity with DashboardForm) ────────────────────────
        public string? GetSelectedModelKey() => _selectedModel?.Key;
        public int GetSelectedMaxTokens() => _selectedModel?.MaxTokens ?? 16384;

        // ─── Native imports for window dragging ─────────────────────────────
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    }
}
