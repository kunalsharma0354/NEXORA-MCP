using System.Drawing.Drawing2D;

namespace NEXORA_MCP
{
    /// <summary>
    /// Login screen — borderless, centered card, modern inputs,
    /// fade-in + error shake animations.
    /// </summary>
    public partial class Form1 : Form
    {
        private const int CardWidth = 360;
        private const int CardHeight = 500;
        private const int FieldInset = 32;   // horizontal + vertical padding of card
        private const int FieldWidth = CardWidth - FieldInset * 2; // 296

        private ModernTextBox _txtUsername = null!;
        private ModernTextBox _txtPassword = null!;
        private AnimatedButton _btnLogin = null!;
        private Label _lblStatus = null!;
        private Panel? _titleBar;
        private Button _btnClose = null!;
        private bool _isSubmitting;

        public Form1()
        {
            InitializeComponent();

            BuildLoginUi();

            // Premium entrance: fade-in + subtle slide-up on load.
            Shown += (_, _) =>
            {
                Opacity = 0;
                int startY = Top;
                Top = startY + 14;
                var timer = new System.Windows.Forms.Timer { Interval = 16 };
                timer.Tick += (_, _) =>
                {
                    if (Opacity < 1)
                    {
                        Opacity = Math.Min(1, Opacity + 0.08);
                        Top = startY + (int)(14 * (1 - Opacity));
                    }
                    else { timer.Stop(); Top = startY; _txtUsername.Focus(); }
                };
                timer.Start();
            };
        }

        private void BuildLoginUi()
        {
            SuspendLayout();

            var controls = new List<Control>();

            // ── Custom title bar (drag region + close) ──
            _titleBar = new Panel
            {
                BackColor = UITheme.Back,
                Dock = DockStyle.Top,
                Height = 32,
                Cursor = Cursors.SizeAll
            };
            _titleBar.MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
                }
            };

            _btnClose = new Button
            {
                Text = "✕",
                Font = UITheme.Body(9),
                ForeColor = UITheme.TextSecondary,
                BackColor = UITheme.Back,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(30, 26),
                Location = new Point(ClientSize.Width - 40, 4),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.MouseEnter += (_, _) => { _btnClose.BackColor = UITheme.CardHover; _btnClose.ForeColor = UITheme.TextPrimary; };
            _btnClose.MouseLeave += (_, _) => { _btnClose.BackColor = UITheme.Back; _btnClose.ForeColor = UITheme.TextSecondary; };
            _btnClose.Click += (_, _) => Close();
            _titleBar.Controls.Add(_btnClose);
            controls.Add(_titleBar);

            // ── Centered login card ──
            int cx = ClientSize.Width / 2;
            int cy = ClientSize.Height / 2;
            int cardX = cx - CardWidth / 2;
            int cardY = cy - CardHeight / 2 + 6;

            var card = new RoundedPanel
            {
                CornerRadius = UITheme.RadiusLarge,
                BackgroundColor = UITheme.BackSecondary,
                BorderColor = UITheme.BorderDefault,
                DrawShadow = true,
                Hoverable = false,
                Size = new Size(CardWidth, CardHeight),
                Location = new Point(cardX, cardY)
            };

            // ── Branding (perfectly centered: full-width labels, TextAlign center) ──
            var lblBrand = new Label
            {
                Text = "NEXORA",
                Font = UITheme.Display(26),
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize = false,
                Size = new Size(CardWidth, 50),
                Location = new Point(0, 44),
                TextAlign = ContentAlignment.MiddleCenter,
                TabStop = false
            };

            var lblSub = new Label
            {
                Text = "MCP  DESKTOP  CLIENT",
                Font = UITheme.BodyBold(8.5),
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                AutoSize = false,
                Size = new Size(CardWidth, 18),
                Location = new Point(0, 96),
                TextAlign = ContentAlignment.MiddleCenter,
                TabStop = false
            };

            // Thin hairline divider under branding (premium 1px).
            var divider = new Panel
            {
                BackColor = UITheme.BorderDefault,
                Size = new Size(160, 1),
                Location = new Point(CardWidth / 2 - 80, 132)
            };
            card.Controls.Add(lblBrand);
            card.Controls.Add(lblSub);
            card.Controls.Add(divider);

            // ── Username ──
            var lblUserCaption = new Label
            {
                Text = "USERNAME",
                Font = UITheme.BodyBold(8.5),
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(FieldInset, 164),
                TabStop = false
            };

            _txtUsername = new ModernTextBox
            {
                PlaceholderText = "Enter username",
                Location = new Point(FieldInset, 190),
                Width = FieldWidth
            };

            // ── Password ──
            var lblPassCaption = new Label
            {
                Text = "PASSWORD",
                Font = UITheme.BodyBold(8.5),
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(FieldInset, 252),
                TabStop = false
            };

            _txtPassword = new ModernTextBox
            {
                PlaceholderText = "Enter password",
                IsPassword = true,
                Location = new Point(FieldInset, 278),
                Width = FieldWidth
            };

            // ── Login button ──
            _btnLogin = new AnimatedButton
            {
                Text = "SIGN IN",
                Font = UITheme.BodyBold(10.5),
                Style = ButtonStyle.Primary,
                Location = new Point(FieldInset, 348),
                Width = FieldWidth,
                Height = 48,
                CornerRadius = UITheme.RadiusSmall + 2,
                AccessibleName = "Sign in"
            };
            _btnLogin.Clicked += async (_, _) => await HandleLoginAsync();

            // ── Status ──
            _lblStatus = new Label
            {
                Text = "",
                Font = UITheme.Body(9),
                ForeColor = UITheme.Error,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                Size = new Size(FieldWidth, 34),
                Location = new Point(FieldInset, 408),
                Visible = false,
                TabStop = false
            };

            // ── Behavior wiring ──
            _txtUsername.EnterKeyPressed += (_, _) => _txtPassword.Focus();
            _txtPassword.EnterKeyPressed += async (_, _) => await HandleLoginAsync();
            KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            card.Controls.Add(lblUserCaption);
            card.Controls.Add(_txtUsername);
            card.Controls.Add(lblPassCaption);
            card.Controls.Add(_txtPassword);
            card.Controls.Add(_btnLogin);
            card.Controls.Add(_lblStatus);

            controls.Add(card);

            // ── Footer ──
            string hwidShort = "";
            try
            {
                var hwid = HwidHelper.GetOrCreateDeviceId();
                if (hwid.Length > 8) hwidShort = $"  ·  {hwid[^8..].ToUpperInvariant()}";
            }
            catch { }
            var lblFooter = new Label
            {
                Text = $"NEXORA MCP v1.0{hwidShort}",
                Font = UITheme.Body(8),
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                AutoSize = false,
                Size = new Size(ClientSize.Width, 18),
                Location = new Point(0, ClientSize.Height - 30),
                TextAlign = ContentAlignment.MiddleCenter,
                TabStop = false
            };
            controls.Add(lblFooter);

            Controls.AddRange(controls.ToArray());
            ResumeLayout(false);
        }

        // ─── Login logic ────────────────────────────────────────────────────
        private void OpenDashboard()
        {
            try
            {
                var dashboard = new DashboardForm();
                dashboard.FormClosed += (_, _) => this.Close();
                this.Hide();
                dashboard.Show();
            }
            catch (Exception)
            {
                // Fall back to the login screen if the dashboard cannot be shown.
            }
        }

        private async Task HandleLoginAsync()
        {
            if (_isSubmitting) return;

            var username = (_txtUsername.Text ?? string.Empty).Trim();
            var password = _txtPassword.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError("Please enter your username.");
                _txtUsername.SetError(true);
                Shake(_txtUsername);
                return;
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("Please enter your password.");
                _txtPassword.SetError(true);
                Shake(_txtPassword);
                return;
            }

            _isSubmitting = true;
            _btnLogin.Text = "SIGNING IN";
            _btnLogin.IsLoading = true;
            _lblStatus.Visible = true;
            _lblStatus.ForeColor = UITheme.TextSecondary;
            _lblStatus.Text = "Authenticating...";

            try
            {
                var hwid = HwidHelper.GetOrCreateDeviceId();
                var result = await AuthService.LoginAsync(username, password, hwid);

                TokenStore.SaveSession(result.Token, result.Username, hwid, result.Expiry, DateTime.UtcNow);
                TokenStore.SaveToDisk();

                _lblStatus.ForeColor = UITheme.Success;
                _lblStatus.Text = $"✓ Signed in as {result.Username}";

                await Task.Delay(500);

                OpenDashboard();
            }
            catch (AuthException ex)
            {
                _lblStatus.ForeColor = UITheme.Error;
                _lblStatus.Text = ex.Message;
            }
            catch (Exception)
            {
                _lblStatus.ForeColor = UITheme.Error;
                _lblStatus.Text = "Something went wrong. Please try again.";
            }
            finally
            {
                _isSubmitting = false;
                _btnLogin.IsLoading = false;
                _btnLogin.Text = "SIGN IN";
            }
        }

        private void ShowError(string message)
        {
            _lblStatus.ForeColor = UITheme.Error;
            _lblStatus.Text = message;
            _lblStatus.Visible = true;
        }

        private void Shake(Control c)
        {
            var original = c.Location;
            AnimationManager.Animate(c, t =>
            {
                float offset = (float)(Math.Sin(t * Math.PI * 6) * 8 * (1 - t));
                c.Location = new Point(original.X + (int)offset, original.Y);
            }, 400, t => t, () => c.Location = original);
        }

        // ─── Native imports for window dragging ─────────────────────────────
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    }
}