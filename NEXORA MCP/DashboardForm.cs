using System.Net.Http.Headers;
using System.Text.Json;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace NEXORA_MCP
{
    /// <summary>
    /// Captures the mouse wheel at the message-queue level (IMessageFilter),
    /// so scrolling the activity log works no matter which control holds focus.
    /// Consumes the wheel and scrolls the feed via ScrollablePanel.ScrollByPixels.
    /// </summary>
    public sealed class WheelRouter : IMessageFilter
    {
        private const int WM_MOUSEWHEEL = 0x020A;
        private readonly Func<Control?> _feed;
        private bool _busy;

        public WheelRouter(Func<Control?> feed) { _feed = feed; }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_MOUSEWHEEL) return false;

            var feed = _feed();
            int delta = 0;
            if (feed == null || feed.IsDisposed)
            {
                WheelDiag.Log("wheel: no feed");
                return false;
            }

            var cursor = Cursor.Position;
            var feedScreen = feed.RectangleToScreen(feed.ClientRectangle);
            bool over = feedScreen.Contains(cursor);
            delta = unchecked((short)((long)m.WParam >> 16));
            WheelDiag.Log($"wheel: delta={delta} cursorOverFeed={over} feedRect={feedScreen} cursor={cursor}");

            if (!over) return false;

            if (delta != 0 && feed is ScrollablePanel sp)
            {
                if (_busy) return true;
                _busy = true;
                try { sp.ScrollByPixels(-(delta / 120) * 50); }
                finally { _busy = false; }
                return true; // consume: never let the native handler double-scroll
            }
            return false;
        }
    }

    /// <summary>
    /// Dashboard — server status card with animated glow, live stats,
    /// and a compact activity log. No chat view.
    /// </summary>
    public partial class DashboardForm : Form
    {
        // ─── Controls ────────────────────────────────────────────────────────
        private Panel _topBar = null!;
        private Panel _selectorPanel = null!;
        private Label _lblModelDisplay = null!;
        private Panel _chevronIcon = null!;
        private Panel _dropdownBox = null!;
        private Panel _contentArea = null!;
        private Panel _dashContent = null!;
        private Panel _statusBar = null!;
        private Label _lblStatus = null!;
        private StatusIndicator _mcpIndicator = null!;
        private ScrollablePanel _activityPanel = null!;
        private Label _lblServerState = null!;
        private Label _lblServerDetail = null!;
        private RoundedPanel _glowPanel = null!;
        private StatCard _cardToolCalls = null!;
        private StatCard _cardSuccess = null!;
        private StatCard _cardLatency = null!;
        private Label _lblActivityEmpty = null!;
        private AnimatedButton _btnLogout = null!;
        private AnimatedButton _btnBrowser = null!;
        private Button _btnMinimize = null!;
        private Button _btnClose = null!;
        private System.Windows.Forms.Timer _refreshTimer = null!;
        private System.Windows.Forms.Timer _glowTimer = null!;

        private EmbeddedBrowserPanel? _browserPanel;
        private bool _browserVisible;

        private bool _serverActive;
        private bool _serverProcessing;
        private string _lastActivityHash = "";
        private float _glowPhase;

        // ─── Model data ──────────────────────────────────────────────────────
        private class AiModel
        {
            public string Id { get; set; } = "";
            public string Key { get; set; } = "";
            public string Name { get; set; } = "";
            public bool Enabled { get; set; }
            public int MaxTokens { get; set; }
            public int Order { get; set; }
            public override string ToString() => Name;
        }

        private List<AiModel> _models = new();
        private AiModel? _selectedModel;
        private bool _modelsLoaded;
        private bool _dropdownOpen;
        private readonly List<Panel> _itemPanels = new();

        // ─── Activity Log ────────────────────────────────────────────────────
        private static readonly string ActivityLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nexora", "mcp_activity.json");

        public DashboardForm()
        {
            InitializeComponent();
            BuildDashboardUi();

            Application.AddMessageFilter(new WheelRouter(() => _activityPanel));
            WheelDiag.Log("DashboardForm created — wheel router installed");

            Load += async (_, _) =>
            {
                await LoadModelsAsync();
                RefreshActivityLog();
                EnsureBrowserPanel();
            };

            // Glow animation timer — only runs when server active/processing
            _glowTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _glowTimer.Tick += (_, _) =>
            {
                _glowPhase = (_glowPhase + 0.02f) % (2 * MathF.PI);
                _glowPanel?.Invalidate();
            };

            _refreshTimer = new System.Windows.Forms.Timer { Interval = 2500 };
            _refreshTimer.Tick += (_, _) => RefreshActivityLog();
            _refreshTimer.Start();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.BackColor = UITheme.Back;
            this.ClientSize = new Size(1080, 720);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.FormBorderStyle = FormBorderStyle.None;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DashboardForm";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "NEXORA — Dashboard";
            this.DoubleBuffered = true;
            this.ResumeLayout(false);
        }

        private void PositionTopBarItems(Label title, Label sub)
        {
            title.Location = new Point(24, 18);
            sub.Location = new Point(title.Right + 12, 24);
            _mcpIndicator.Location = new Point(sub.Right + 20, 17);
        }

        private void BuildDashboardUi()
        {
            SuspendLayout();

            // ══ TOP BAR ══════════════════════════════════════════════════════
            _topBar = new Panel
            {
                BackColor = UITheme.TopBar,
                Dock = DockStyle.Top,
                Height = 56,
                Padding = new Padding(24, 0, 24, 0)
            };
            _topBar.Paint += (_, e) =>
            {
                using var pen = new Pen(UITheme.Border);
                e.Graphics.DrawLine(pen, 0, _topBar.Height - 1, _topBar.Width, _topBar.Height - 1);
            };

            var lblTitle = new Label
            {
                Text = "NEXORA",
                Font = UITheme.Heading(16),
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(24, 18)
            };

            var lblSubTitle = new Label
            {
                Text = "MCP DASHBOARD",
                Font = UITheme.Body(9.5),
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(lblTitle.Width + 30, 24)
            };

            _mcpIndicator = new StatusIndicator
            {
                LabelText = "MCP: Checking...",
                State = ServerState.Connecting,
                Location = new Point(lblTitle.Width + lblSubTitle.Width + 46, 13),
                Size = new Size(200, 30),
                AccessibleName = "MCP server status"
            };

            BuildModelSelector();

            _btnLogout = new AnimatedButton
            {
                Text = "LOGOUT",
                Font = UITheme.BodyBold(9),
                Style = ButtonStyle.Ghost,
                Size = new Size(92, 34),
                CornerRadius = 8
            };
            _btnLogout.Clicked += (_, _) => Logout();

            _btnBrowser = new AnimatedButton
            {
                Text = "BROWSER",
                Font = UITheme.BodyBold(9),
                Style = ButtonStyle.Ghost,
                Size = new Size(96, 34),
                CornerRadius = 8
            };
            _btnBrowser.Clicked += (_, _) => ToggleBrowser();

            // ── Custom window buttons (borderless, like the login screen) ──
            _btnMinimize = MakeTitleButton("\u2013");
            _btnMinimize.Click += (_, _) => WindowState = FormWindowState.Minimized;

            _btnClose = MakeTitleButton("\u2715");
            _btnClose.Click += (_, _) => Close();

            // Draggable title bar
            _topBar.MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
                }
            };

            _topBar.Controls.AddRange(new Control[] { lblTitle, lblSubTitle, _mcpIndicator, _selectorPanel, _btnBrowser, _btnLogout, _btnMinimize, _btnClose });
            _topBar.Resize += (_, _) => UpdateSelectorPosition();
            PositionTopBarItems(lblTitle, lblSubTitle);

            // ══ STATUS BAR ════════════════════════════════════════════════════
            _statusBar = new Panel
            {
                BackColor = UITheme.TopBar,
                Dock = DockStyle.Bottom,
                Height = 36,
                Padding = new Padding(24, 0, 24, 0)
            };
            _statusBar.Paint += (_, e) =>
            {
                using var pen = new Pen(UITheme.Border);
                e.Graphics.DrawLine(pen, 0, 0, _statusBar.Width, 0);
            };

            _lblStatus = new Label
            {
                Text = "NEXORA MCP · Dashboard ready",
                Font = UITheme.Body(9),
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(24, 11)
            };

            _statusBar.Controls.Add(_lblStatus);

            // ══ CONTENT AREA ══════════════════════════════════════════════════
            _contentArea = new Panel
            {
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill,
                Padding = new Padding(32, 20, 32, 20)
            };

            // The dashboard widgets live in _dashContent; the embedded browser sits
            // beneath it (always visible so WebView2 keeps rendering) and is shown
            // on top via z-order when the user presses BROWSER.
            _dashContent = new Panel
            {
                BackColor = UITheme.Back,
                Dock = DockStyle.Fill
            };

            // ── Server Status Card (large, animated glow) ──
            _glowPanel = new RoundedPanel
            {
                CornerRadius = UITheme.RadiusLarge,
                BackgroundColor = UITheme.BackSecondary,
                BorderColor = UITheme.BorderDefault,
                Hoverable = false,
                Dock = DockStyle.Top,
                Height = 158
            };
            _glowPanel.Paint += OnStatusCardPaint;

            var lblServerTitle = new Label
            {
                Text = "M C P   S E R V E R",
                Font = UITheme.BodyBold(10),
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(36, 22)
            };

            _lblServerState = new Label
            {
                Text = "CHECKING",
                Font = UITheme.Heading(28),
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(36, 50)
            };

            _lblServerDetail = new Label
            {
                Text = "Connecting to MCP activity feed...",
                Font = UITheme.Body(9.5),
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(38, 112)
            };

            _glowPanel.Controls.AddRange(new Control[] { lblServerTitle, _lblServerState, _lblServerDetail });

            // ── Stats row ──
            var statsPanel = new Panel
            {
                BackColor = Color.Transparent,
                Dock = DockStyle.Top,
                Height = 124,
                Padding = new Padding(0, 16, 0, 0)
            };

            _cardToolCalls = new StatCard
            {
                Title = "TOTAL TOOL CALLS",
                Dock = DockStyle.Left,
                Width = 200,
                Accent = Color.White
            };
            _cardSuccess = new StatCard
            {
                Title = "SUCCESS RATE",
                ShowRing = true,
                Dock = DockStyle.Left,
                Width = 220,
                Accent = UITheme.Success,
                Margin = new Padding(16, 0, 0, 0)
            };
            _cardLatency = new StatCard
            {
                Title = "AVG LATENCY",
                Dock = DockStyle.Left,
                Width = 200,
                Accent = UITheme.Warning,
                Margin = new Padding(16, 0, 0, 0)
            };

            statsPanel.Controls.Add(_cardLatency);
            statsPanel.Controls.Add(_cardSuccess);
            statsPanel.Controls.Add(_cardToolCalls);

            // ── Activity Log ──
            var logPanel = new Panel
            {
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 20, 0, 0)
            };

            var lblLogHeader = new Label
            {
                Text = "RECENT ACTIVITY",
                Font = UITheme.BodyBold(10),
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(2, 10)
            };

            // Header gets its own fixed strip so it can never be covered by cards.
            var logHeaderStrip = new Panel
            {
                BackColor = Color.Transparent,
                Dock = DockStyle.Top,
                Height = 30
            };
            logHeaderStrip.Controls.Add(lblLogHeader);

            _activityPanel = new ScrollablePanel
            {
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill
            };

            _lblActivityEmpty = new Label
            {
                Text = "No tool calls yet — activity will appear here when OpenCode calls NEXORA.",
                Font = UITheme.Body(10),
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(6, 8)
            };

            _activityPanel.Controls.Add(_lblActivityEmpty);
            logPanel.Controls.Add(_activityPanel);
            logPanel.Controls.Add(logHeaderStrip);

            _dashContent.Controls.Add(logPanel);
            _dashContent.Controls.Add(statsPanel);
            _dashContent.Controls.Add(_glowPanel);
            _contentArea.Controls.Add(_dashContent);

            Controls.AddRange(new Control[] { _contentArea, _statusBar, _topBar, _dropdownBox });

            // Close dropdown when clicking outside selector/dropdown area
            _contentArea.Click += (_, _) => { if (_dropdownOpen) CloseDropdown(); };
            _statusBar.Click += (_, _) => { if (_dropdownOpen) CloseDropdown(); };

            ResumeLayout(false);
        }

        private void Logout()
        {
            TokenStore.ClearSession();
            var login = new Form1();
            this.Hide();
            login.FormClosed += (_, _) => this.Close();
            login.Show();
        }

        // ── Embedded browser toggle ─────────────────────────────────────────
        private void ToggleBrowser()
        {
            EnsureBrowserPanel();
            _browserVisible = !_browserVisible;
            if (_browserVisible)
            {
                _browserPanel!.BringToFront();
                _lblStatus.Text = "NEXORA MCP · Browser: embedded — AI and user share this window";
            }
            else
            {
                _dashContent.BringToFront();
                _lblStatus.Text = "NEXORA MCP · Dashboard ready";
            }
            _contentArea.Invalidate();
        }

        private void EnsureBrowserPanel()
        {
            if (_browserPanel != null) return;
            _browserPanel = new EmbeddedBrowserPanel
            {
                Dock = DockStyle.Fill,
                Visible = true
            };
            _contentArea.Controls.Add(_browserPanel);
            // Keep it behind the dashboard widgets by default (z-order), while
            // staying visible so WebView2 keeps rendering for the MCP agent.
            _browserPanel.SendToBack();
            _browserPanel.CreateControl();
            // Initialize the WebView2 core + bridge on the UI thread so the
            // MCP agent can drive the browser even before the user toggles it in.
            _ = _browserPanel.EnsureInitializedAsync();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  SERVER STATUS CARD PAINT (animated glow)
        // ════════════════════════════════════════════════════════════════════════

        private void OnStatusCardPaint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.CompositingQuality = CompositingQuality.HighQuality;

            if (_glowPanel == null) return;

            // Animated radial glow when server is active/processing
            if (_serverActive || _serverProcessing)
            {
                var cx = _glowPanel.Width / 2;
                var cy = _glowPanel.Height / 2;
                float baseRadius = Math.Min(_glowPanel.Width, _glowPanel.Height) * 0.55f;
                float pulse = 0.5f + 0.5f * (float)Math.Sin(_glowPhase * 2f);
                float radius = baseRadius + pulse * 40f;
                if (_serverProcessing) radius += 10f;

                Color glow = _serverProcessing ? UITheme.Warning : UITheme.Success;
                using var path = new GraphicsPath();
                path.AddEllipse(cx - radius, cy - radius, radius * 2, radius * 2);

                using var wrap = new PathGradientBrush(path)
                {
                    CenterColor = Color.FromArgb(35, glow),
                    SurroundColors = new[] { Color.Transparent },
                    CenterPoint = new PointF(cx, cy)
                };
                g.FillPath(wrap, path);
            }

            // Accent underline — proportional width (40% of panel width, max 200px)
            Color lineColor = _serverProcessing ? UITheme.Warning : UITheme.Success;
            int lineWidth = Math.Min(200, _glowPanel.Width * 40 / 100);
            using (var linePen = new Pen(lineColor, 2.5f))
                g.DrawLine(linePen, 36, _glowPanel.Height - 3, 36 + lineWidth, _glowPanel.Height - 3);
        }

        private void UpdateServerState()
        {
            string state = _serverProcessing ? "PROCESSING" : _serverActive ? "ACTIVE" : "INACTIVE";
            _lblServerState.Text = state;
            _lblServerState.ForeColor = _serverProcessing ? UITheme.Warning : _serverActive ? UITheme.TextPrimary : UITheme.Error;

            // Start/stop glow timer based on server state
            if (_serverActive || _serverProcessing)
            {
                if (!_glowTimer.Enabled) _glowTimer.Start();
            }
            else
            {
                _glowTimer.Stop();
                _glowPhase = 0;
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  CUSTOM MODEL SELECTOR
        // ════════════════════════════════════════════════════════════════════════

        private void BuildModelSelector()
        {
            _selectorPanel = new Panel
            {
                BackColor = UITheme.Card,
                Size = new Size(280, 40),
                Cursor = Cursors.Hand
            };

            _lblModelDisplay = new Label
            {
                Text = "Select model",
                Font = UITheme.Body(10),
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Padding = new Padding(14, 0, 38, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _chevronIcon = new Panel
            {
                BackColor = Color.Transparent,
                Size = new Size(36, 40),
                Dock = DockStyle.Right,
                Cursor = Cursors.Hand
            };
            _chevronIcon.Paint += (_, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var cxx = 14;
                var cyy = _chevronIcon.Height / 2;
                using var brush = new SolidBrush(UITheme.TextSecondary);
                g.FillPolygon(brush, new[]
                {
                    new Point(cxx - 5, cyy - 3),
                    new Point(cxx + 5, cyy - 3),
                    new Point(cxx, cyy + 4)
                });
            };

            _selectorPanel.Paint += (_, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, _selectorPanel.Width - 1, _selectorPanel.Height - 1);
                using var path = UITheme.RoundedRect(rect, 9);
                using var pen = new Pen(_dropdownOpen ? UITheme.BorderHi : UITheme.Border, 1);
                g.DrawPath(pen, path);
            };

            _selectorPanel.Click += (_, _) => ToggleDropdown();
            _lblModelDisplay.Click += (_, _) => ToggleDropdown();
            _chevronIcon.Click += (_, _) => ToggleDropdown();

            _selectorPanel.MouseEnter += (_, _) =>
            {
                _selectorPanel.BackColor = UITheme.CardHover;
                _selectorPanel.Invalidate();
            };
            _selectorPanel.MouseLeave += (_, _) =>
            {
                if (!_dropdownOpen) _selectorPanel.BackColor = UITheme.Card;
                _selectorPanel.Invalidate();
            };

            _selectorPanel.Controls.Add(_lblModelDisplay);
            _selectorPanel.Controls.Add(_chevronIcon);

            _dropdownBox = new Panel
            {
                BackColor = UITheme.Card,
                Visible = false,
                Size = new Size(280, 0),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _dropdownBox.Paint += (_, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = UITheme.RoundedRect(new Rectangle(0, 0, _dropdownBox.Width - 1, _dropdownBox.Height - 1), 10);
                using var pen = new Pen(UITheme.Border, 1);
                g.DrawPath(pen, path);
            };
        }

        private void UpdateSelectorPosition()
        {
            // Right-aligned layout (no overlaps): selector · BROWSER · LOGOUT · [—] [×]
            _selectorPanel.Location = new Point(_topBar.Width - 584, 8);
            _btnBrowser.Location = new Point(_topBar.Width - 292, 11);
            _btnLogout.Location = new Point(_topBar.Width - 180, 11);
            _btnMinimize.Location = new Point(_topBar.Width - 76, 17);
            _btnClose.Location = new Point(_topBar.Width - 40, 17);
            _dropdownBox.Location = new Point(_selectorPanel.Left, _selectorPanel.Bottom + 4);
        }

        private void ToggleDropdown()
        {
            if (_dropdownOpen) CloseDropdown();
            else OpenDropdown();
        }

        private void OpenDropdown()
        {
            if (_models.Count == 0) return;
            _dropdownOpen = true;
            _selectorPanel.BackColor = UITheme.CardHover;
            _selectorPanel.Invalidate();

            foreach (var p in _itemPanels) p.Dispose();
            _itemPanels.Clear();
            _dropdownBox.Controls.Clear();

            int itemHeight = 44;
            int totalHeight = _models.Count * itemHeight + 8;

            for (int i = 0; i < _models.Count; i++)
            {
                var model = _models[i];
                var isSelected = model == _selectedModel;
                var itemPanel = CreateDropdownItem(model, i, itemHeight, isSelected);
                itemPanel.Location = new Point(4, 4 + i * itemHeight);
                _dropdownBox.Controls.Add(itemPanel);
                _itemPanels.Add(itemPanel);
            }

            var selectorLoc = _selectorPanel.PointToScreen(Point.Empty);
            var formLoc = this.PointToScreen(Point.Empty);
            int dropX = selectorLoc.X - formLoc.X;
            int dropY = selectorLoc.Y - formLoc.Y + _selectorPanel.Height + 4;

            _dropdownBox.Size = new Size(280, totalHeight);
            _dropdownBox.Location = new Point(dropX, dropY);
            _dropdownBox.Visible = true;
            _dropdownBox.BringToFront();
        }

        private void CloseDropdown()
        {
            _dropdownOpen = false;
            _selectorPanel.BackColor = UITheme.Card;
            _selectorPanel.Invalidate();
            _dropdownBox.Visible = false;
        }

        private Panel CreateDropdownItem(AiModel model, int index, int height, bool isSelected)
        {
            var panel = new Panel
            {
                BackColor = Color.Transparent,
                Size = new Size(272, height),
                Cursor = Cursors.Hand,
                Tag = model
            };

            var lblName = new Label
            {
                Text = model.Name ?? "",
                Font = UITheme.Body(10),
                ForeColor = isSelected ? Color.White : UITheme.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize = false,
                Location = new Point(16, 4),
                Size = new Size(240, 22)
            };

            var lblKey = new Label
            {
                Text = model.Key ?? "",
                Font = UITheme.Body(8),
                ForeColor = isSelected ? Color.FromArgb(200, 200, 200) : UITheme.TextSecondary,
                BackColor = Color.Transparent,
                AutoSize = false,
                Location = new Point(16, 26),
                Size = new Size(240, 16)
            };

            panel.Controls.Add(lblName);
            panel.Controls.Add(lblKey);

            panel.Paint += (_, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (panel.BackColor != Color.Transparent)
                {
                    using var bgPath = UITheme.RoundedRect(new Rectangle(0, 0, panel.Width, panel.Height), 8);
                    using var bgBrush = new SolidBrush(panel.BackColor);
                    g.FillPath(bgBrush, bgPath);
                }
                if (isSelected)
                {
                    using var barBrush = new SolidBrush(Color.White);
                    g.FillRectangle(barBrush, 0, 8, 3, panel.Height - 16);
                }
            };

            Action<object?, EventArgs> enterHandler = (_, _) => { panel.BackColor = UITheme.CardHover; panel.Invalidate(); };
            Action<object?, EventArgs> leaveHandler = (_, _) => { panel.BackColor = Color.Transparent; panel.Invalidate(); };
            Action<object?, EventArgs> clickHandler = (_, _) => { SelectModel(model); CloseDropdown(); };

            panel.MouseEnter += new EventHandler(enterHandler);
            panel.MouseLeave += new EventHandler(leaveHandler);
            panel.Click += new EventHandler(clickHandler);
            lblName.MouseEnter += new EventHandler(enterHandler);
            lblName.MouseLeave += new EventHandler(leaveHandler);
            lblName.Click += new EventHandler(clickHandler);
            lblKey.MouseEnter += new EventHandler(enterHandler);
            lblKey.MouseLeave += new EventHandler(leaveHandler);
            lblKey.Click += new EventHandler(clickHandler);

            return panel;
        }

        private void SelectModel(AiModel model)
        {
            _selectedModel = model;
            _lblModelDisplay.Text = model.Name ?? "";
            _lblModelDisplay.ForeColor = UITheme.TextPrimary;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  LOAD MODELS
        // ════════════════════════════════════════════════════════════════════════

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
                            Id = m.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                            Key = m.TryGetProperty("key", out var key) ? key.GetString() ?? "" : "",
                            Name = m.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                            Enabled = m.TryGetProperty("enabled", out var en) && en.GetBoolean(),
                            MaxTokens = m.TryGetProperty("maxTokens", out var mt) ? mt.GetInt32() : 16384,
                            Order = m.TryGetProperty("order", out var ord) ? ord.GetInt32() : 0
                        };
                        if (model.Enabled && !string.IsNullOrEmpty(model.Key))
                            _models.Add(model);
                    }
                }

                _models = _models.OrderBy(m => m.Order).ToList();
                BeginInvoke(() => { if (_models.Count > 0) SelectModel(_models[0]); });
                _modelsLoaded = true;
            }
            catch { AddDefaultModel(); }
        }

        private void AddDefaultModel()
        {
            _models.Add(new AiModel
            {
                Key = "nvidia/nemotron-3-ultra-550b-a55b",
                Name = "Nemotron-3 Ultra 550B",
                Enabled = true, MaxTokens = 16384, Order = 0
            });
            BeginInvoke(() => SelectModel(_models[0]));
            _modelsLoaded = true;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  ACTIVITY LOG (non-chat cards)
        // ════════════════════════════════════════════════════════════════════════

        private class ActivityEntry
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

        private void RefreshActivityLog()
        {
            try
            {
                if (_activityPanel == null || IsDisposed) return;

                List<ActivityEntry>? entries = null;
                if (File.Exists(ActivityLogPath))
                {
                    var json = File.ReadAllText(ActivityLogPath);
                    entries = JsonSerializer.Deserialize<List<ActivityEntry>>(json);
                }

                bool anyActivity = entries is { Count: > 0 };
                var toolEntries = entries?.Where(e => e.Type == "tool_call").ToList() ?? new List<ActivityEntry>();
                var pendingEntry = toolEntries.FirstOrDefault(e => e.Status == "pending");

                _serverActive = TokenStore.IsLoggedIn;
                _serverProcessing = pendingEntry != null;

                _mcpIndicator.State = _serverProcessing ? ServerState.Connecting : ServerState.Active;
                _mcpIndicator.LabelText = _serverProcessing ? "MCP: Processing..." : "MCP: Active";

                int toolCalls = toolEntries.Count;
                int successCount = toolEntries.Count(e => e.Success && e.Status != "pending");
                int successRate = toolCalls > 0 ? (int)((double)successCount / toolCalls * 100) : 0;
                int latencyMs = EstimateLatency(toolEntries);

                if (!_serverProcessing)
                {
                    _cardToolCalls.SetCounter(toolCalls);
                    _cardSuccess.Progress = successRate / 100f;
                    _cardLatency.SetCounter(latencyMs, " ms");
                }

                UpdateServerState();

                if (!anyActivity)
                {
                    _lblServerDetail.Text = "No tool calls yet — server is idle and waiting.";
                    _activityPanel.Controls.Clear();
                    _activityPanel.Controls.Add(_lblActivityEmpty);
                    _lblActivityEmpty.Visible = true;
                    _lblStatus.Text = "NEXORA MCP · Server active — waiting for tool calls";
                    return;
                }

                _lblActivityEmpty.Visible = false;

                if (_serverProcessing)
                {
                    _lblServerDetail.Text = $"Processing \"{Truncate(pendingEntry!.Input ?? "", 70)}\"...";
                    _lblStatus.Text = "MCP Server: Processing tool call...";
                }
                else
                {
                    _lblServerDetail.Text = $"Last call: {FormatTime(toolEntries.LastOrDefault()?.Timestamp)} · Server is idle";
                    _lblStatus.Text = $"MCP Server: Active — {toolCalls} calls · {successRate}% success";
                }

                RenderActivityCards(toolEntries);
            }
            catch { }
        }

        private void RenderActivityCards(List<ActivityEntry> toolEntries)
        {
            var recent = toolEntries.TakeLast(15).Reverse().ToList();
            var hash = string.Join("|", recent.Select(e => $"{e.Timestamp}|{e.Status}|{e.Success}"));
            if (hash == _lastActivityHash && !_serverProcessing) return;
            _lastActivityHash = hash;

            _activityPanel.SuspendLayout();
            _activityPanel.Controls.Clear();

            int cardWidth = Math.Max(200, _activityPanel.ClientSize.Width - 8 - 6);
            int y = 8;
            foreach (var entry in recent)
            {
                var card = new ActivityLogCard
                {
                    ToolName = string.IsNullOrEmpty(entry.Tool) ? "tool_call" : entry.Tool,
                    Timestamp = FormatTime(entry.Timestamp),
                    Success = entry.Success,
                    IsPending = entry.Status == "pending",
                    InputPreview = entry.Input ?? "",
                    Model = entry.Model ?? "",
                    FullJson = JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = true }),
                    Size = new Size(cardWidth, 86)
                };
                _activityPanel.AddCard(card, y);
                y += card.Height + 10;
            }

            int contentHeight = y + 8;
            _activityPanel.SetContentHeight(contentHeight);
            _activityPanel.ResumeLayout(false);

            WheelDiag.Log($"rendered={recent.Count} cards contentH={contentHeight} clientH={_activityPanel.ClientSize.Height}");
        }

        private int EstimateLatency(List<ActivityEntry> entries)
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

        // ─── Public API ──────────────────────────────────────────────────────
        public string? GetSelectedModelKey() => _selectedModel?.Key;
        public int GetSelectedMaxTokens() => _selectedModel?.MaxTokens ?? 16384;

        // ─── Mouse-wheel routing ─────────────────────────────────────────────
        private const int WM_MOUSEWHEEL = 0x020A;

        private Button MakeTitleButton(string glyph)
        {
            var b = new Button
            {
                Text = glyph,
                Font = UITheme.Body(9),
                ForeColor = UITheme.TextSecondary,
                BackColor = UITheme.TopBar,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(30, 26),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.MouseEnter += (_, _) => { b.BackColor = UITheme.CardHover; b.ForeColor = UITheme.TextPrimary; };
            b.MouseLeave += (_, _) => { b.BackColor = UITheme.TopBar; b.ForeColor = UITheme.TextSecondary; };
            return b;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        protected override void WndProc(ref Message m)
        {
            // Whatever has focus, unhandled wheel events bubble up to the form.
            // If the cursor is over the activity feed, scroll it manually.
            if (m.Msg == WM_MOUSEWHEEL && _activityPanel != null && !_activityPanel.IsDisposed)
            {
                var pt = _activityPanel.PointToClient(Cursor.Position);
                WheelDiag.Log($"form.WndProc wheel: delta={(short)((long)m.WParam >> 16)} overFeed={_activityPanel.ClientRectangle.Contains(pt)}");
                if (_activityPanel.ClientRectangle.Contains(pt))
                {
                    int delta = unchecked((short)((long)m.WParam >> 16));
                    _activityPanel.ScrollByPixels(-(delta / 120) * 50);
                    m.Result = IntPtr.Zero;
                    return;
                }
            }
            base.WndProc(ref m);
        }
    }
}