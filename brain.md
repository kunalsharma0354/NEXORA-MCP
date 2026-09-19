# NEXORA MCP — Project Brain

> **Single source of truth. Read this file to understand the entire project — no need to read source files.**
> *Last verified from actual code: 2026-09-10* | *Browser feature migrated to embedded WebView2 + tested: 2026-09-10*

---

## 1. Project Overview

**NEXORA MCP** = Windows desktop app + MCP server, but **ONE binary** (single WinForms project, `net8.0-windows`).

It is a **"Prompt Intelligence Layer"** for **OpenCode**:

1. **GUI Mode (default)** — Dark-themed login screen + live dashboard. User signs in → gets a bearer token → token saved to disk so the MCP server can reuse it. Dashboard shows MCP server activity, stats, and lets you pick an AI model.
2. **MCP Server Mode (`--mcp` flag)** — Same `.exe` runs as an **MCP stdio server** for OpenCode. Exposes tools:
   - `nexora_enhance_prompt` → calls NEXORA AI API to turn a short/vague request into a detailed, execution-ready prompt for a coding agent.
   - **`nexora_browser_*` → Embedded Browser**: a **WebView2 browser embedded INSIDE the app window** (dashboard `BROWSER` toggle) that BOTH the user and the AI agent control in real time. NO separate browser process/hidden — the browser lives in the GUI; the MCP server drives it over a loopback bridge. Profile persists → logins (Gmail etc.) survive restarts.

**Backend**: Remote Vercel-hosted Node API — `https://nexora-navy-omega.vercel.app`
- `POST /api/auth-login` — HMAC-signed JWT, 30-day validity
- `POST /api/ai` — NVIDIA Nemotron (server-side NVIDIA key), JSON or SSE
- `GET /api/ai-models` — available model list

**Key architecture insight**: GUI writes the session to disk; the separately-spawned MCP server process reads the same file. Dashboard polls the shared activity log file the MCP server writes. **Cross-process communication = plain JSON files in `%LOCALAPPDATA%\Nexora\` + a loopback TCP bridge (embedded browser only), no other IPC channels.**

---

## 2. Directory Tree (Actual, Verified)

```
NEXORA MCP\
├── brain.md                        # THIS file (architecture brain)
├── NEXORA MCP.sln                  # VS solution (single project)
├── NEXORA_AI_API_Integration.md    # API integration guide (Hinglish)
├── sysprompot.txt                  # System prompt (root copy — MCP servers load it)
├── .vs\                            # VS cache — ignore
└── NEXORA MCP\                     # THE ONLY project (GUI + MCP server both live here)
    ├── NEXORA MCP.csproj           # WinExe, net8.0-windows, nullable enabled
    ├── Program.cs                  # Entry point + MCP hosting + the MCP tool itself
    ├── Form1.cs                    # Login form (UI built in code)
    ├── Form1.Designer.cs           # Form properties only
    ├── Form1.resx                  # Empty resx
    ├── DashboardForm.cs            # Dashboard: status, stats, model picker, activity feed
    ├── AuthService.cs              # HTTP client → /api/auth-login
    ├── NexoraAiClient.cs           # HTTP client → /api/ai (prompt enhancement)
    ├── TokenStore.cs               # Session holder + disk persistence
    ├── HwidHelper.cs               # Persistent per-machine device ID
    ├── OpenCodeSetup.cs            # Auto-register MCP server in OpenCode config
    ├── ActivityLogger.cs           # Cross-process activity log writer
    ├── UITheme.cs                  # The entire design system (colors/fonts/easing)
    ├── AnimationManager.cs         # Single 60fps timer driving all animations
    ├── AnimatedButton.cs           # Rounded button (Primary/Ghost/Danger + spinner)
    ├── ModernTextBox.cs            # TextBox + placeholder + animated focus/error border
    ├── RoundedPanel.cs             # Rounded panel w/ optional shadow + border
    ├── ScrollablePanel.cs          # Manual scroll container w/ custom scrollbar
    ├── ActivityLogCard.cs          # One activity entry card
    ├── StatCard.cs                 # Animated counter + progress ring card
    ├── StatusIndicator.cs          # Pulsing status dot/pill
    ├── ModelSelector.cs            # Standalone dropdown control (CURRENTLY UNUSED)
    ├── EmbeddedBrowserPanel.cs     # ★ WebView2 browser UserControl (inside dashboard) — CDP ops, KeyMap, bridge server
    ├── BrowserBridge.cs            # ★ LoopbackBridgeServer + BridgeClient (TCP) — drives the embedded browser from MCP process
    ├── BrowserSession.cs           # ★ MCP-side bridge client — lifecycle, proxy, tabs, concurrency gate
    ├── NexoraBrowserTool.cs        # ★ MCP tools nexora_browser_* (navigate/click/type/content/screenshot/proxy...)
    ├── WheelDiag.cs                # TEMP wheel-scroll debug logger (production code calls it)
    ├── sysprompot.txt              # System prompt copy (→ output dir)
    ├── obj\ / bin\                 # Build artifacts — ignore
```

> ⚠️ **Stale in old brain.md / fixed now**: There is **NO separate `NexoraMcp/` console project**. The MCP server is embedded in the WinForms project via the `--mcp` flag. `NexoraAiClient.LoadSystemPrompt` still has a legacy `NexoraMcp/` folder fallback path — harmless.

---

## 3. File-by-File Responsibilities

### Core (entry / logic)

| File | Role |
|---|---|
| **Program.cs** | `[STAThread] Main`. `args[0]=="--mcp"` → `RunMcpServer()`; else UI. MCP mode: auto-register with OpenCode → log event → `TokenStore.LoadFromDisk()` → hosted MCP server (stdio) with `WithToolsFromAssembly()`. Also defines `NexoraEnhancePromptTool` (`[McpServerToolType]` static) with tool **`nexora_enhance_prompt(string prompt, string model=...)`**. |
| **Form1.cs** | Login form. `BuildLoginUi()` builds everything programmatically: draggable title bar, centered 360×500 card, NEXORA brand, 2 `ModernTextBox` (user/pass), `AnimatedButton` SIGN IN, footer w/ HWID. Fade-in+slide-up on show, Enter=login, Escape=close, error shake. `HandleLoginAsync()` → `AuthService` → `TokenStore.SaveSession` + `SaveToDisk` → `OpenDashboard()`. **Auto-resume**: ctor does `TokenStore.LoadFromDisk(); if (TokenStore.IsLoggedIn) Shown += OpenDashboard;` — valid saved session skips login. |
| **DashboardForm.cs** | 1080×720 borderless dashboard. `WheelRouter : IMessageFilter` intercepts `WM_MOUSEWHEEL` globally for the feed. Top bar (title, `StatusIndicator`, model selector, **BROWSER** toggle, LOGOUT, min/close). Server-status card with animated radial glow (16ms timer). Stats row (`StatCard`s: TOTAL TOOL CALLS, SUCCESS RATE, AVG LATENCY). Activity feed = `ScrollablePanel` of `ActivityLogCard`s, refreshed every 2.5s. `LoadModelsAsync()` → `GET /api/ai-models`. `RefreshActivityLog()` reads `mcp_activity.json`. Hosts the embedded browser: `_browserPanel` (Dock=Fill, `Visible=true`, **SendToBack**) + opaque `_dashContent` wrap over the widgets; `ToggleBrowser()` toggles `BringToFront()` so the browser stays rendering (screenshots work while hidden behind the dashboard); `EnsureBrowserPanel()` lazily creates + `EnsureInitializedAsync()` on Load. Public API: `GetSelectedModelKey()`, `GetSelectedMaxTokens()`. |
| **BrowserSession.cs** | ★ **MCP-side bridge client** (singleton in the `--mcp` DI container). `BridgeClient` → status-file port discovery. Serializes ALL ops via `SemaphoreSlim _gate`. Result records: `LaunchResult, NavResult, ContentResult, ScreenshotResult, ActionResult, WaitResult, SetProxyResult, TabInfo` (PascalCase JSON). App not running → friendly `BridgeClient.NeedAppMessage` error ("Open the NEXORA MCP app…"). |
| **AuthService.cs** | Static. `LoginAsync(u,p,hwid)` → `POST /api/auth-login`, 30s timeout. Friendly error mapping: 400 invalid request / 401 wrong credentials / 403 blocked / 429 uses `Retry-After` / 5xx server error / timeout / network. Returns `LoginResult(token, username, expiry)`. |
| **NexoraAiClient.cs** | `HttpClient`, 5-min timeout for `/api/ai`. Loads system prompt from `sysprompot.txt` (base dir → CWD → `NexoraMcp/` fallback → inline default). `EnhancePromptAsync(...)` posts `{userPrompt, model, systemPrompt, thinking:true, temperature:0.7, maxTokens:16384, responseFormat:"json"}` with Bearer token. Parses multi-shape response (`content` / `choices[0].message.content` / `response` / `text` / raw). |
| **TokenStore.cs** | Static session. `BaseUrl = "https://nexora-navy-omega.vercel.app"`. In-memory token state + disk persistence to `%LOCALAPPDATA%\Nexora\session.json` (`SaveToDisk`, `LoadFromDisk` — deletes file if expired, `DeleteFromDisk`, `ClearSession`). Decodes JWT `exp` manually (Base64Url + JsonDocument). All disk ops fail-silently. |
| **HwidHelper.cs** | Stable per-machine GUID from `%LOCALAPPDATA%\Nexora\device.id` (created once, GUID no-dashes). Falls back to in-memory GUID. |
| **OpenCodeSetup.cs** | `IsOpenCodeInstalled()` (checks `%LOCALAPPDATA%\opencode\opencode.exe`, ProgramFiles x86/x64, `where opencode`). `GetConfigPath()` → `~/.config/opencode/opencode.json` then `.jsonc`. `IsMcpRegistered()` checks `mcp`/`mcpServers` for `nexora`. `RegisterMcpServer()` rewrites config, merges both sections into `mcp`, adds `{ type:"local", command:[<exePath>, "--mcp"], enabled:true }`. `TryRestartOpenCode()` restarts OpenCode processes. |
| **ActivityLogger.cs** | Static, thread-safe (lock). Writes `%LOCALAPPDATA%\Nexora\mcp_activity.json`. `LogPending` (Status="pending" → dashboard shows PROCESSING) → `CompletePending` (Status="done", Success/Error). Keeps last **100** entries; input truncated 500 chars, output 2000. |

### UI Component Library (custom-drawn, all pull from UITheme)

| File | Role |
|---|---|
| **EmbeddedBrowserPanel.cs** | ★ UserControl hosting the **WebView2 browser** + address/action toolbar. Op router (`HandleOpAsync`) executes bridge ops on the WebView2: navigate, content, screenshot, click, fill, type, press_key, evaluate, wait_for, tabs, set_proxy, close. `EnsureInitializedAsync()` (idempotent: creates WebView2 core + starts `LoopbackBridgeServer` + writes status file; called on panel `Load` and eagerly by dashboard). Proxy = dispose + recreate WebView2 (same UserDataFolder). Includes `KeyMap` (key/char → CDP VK). Navigates via CDP `Input.dispatchMouseEvent/E insertText/E dispatchKeyEvent`, `Page.captureScreenshot` (fallback CapturePreviewAsync). |
| **BrowserBridge.cs** | ★ `BrowserSession`-side client (`BridgeClient.WriteAsync/ReadAllAsync`, 4-byte LE length prefix) + GUI-side `LoopbackBridgeServer` (async `TcpListener`, JSON op → Task → response; `IsRunning`; master `Cancel` token stops server on panel detach). Op result types live in `BrowserSession.cs`. `NeedAppMessage` = "Open the NEXORA MCP app…" when no service on the port. |
| **UITheme.cs** | Design system. Base #0A0A0A, #111, #181; borders #2A/#3A; text grays; accents success #00D47E, warning #FFB800, error #FF4444. Fonts (Segoe UI/Consolas), easings (EaseOutCubic, EaseInOutSine, EaseOutExpo), spacing (4px), radii (6/10/14/999). Helpers: `Lerp`, `RoundedRect`, `DrawFocusRing`, `DrawDivider`. Legacy color aliases kept for compat. |
| **AnimationManager.cs** | One shared 16ms timer; `Animation` objects (Target, Update, Easing, Duration, StartMs, OnComplete). `Animate()`, `AnimateInt`, `AnimateFloat` — UI-thread-safe via `BeginInvoke`. |
| **AnimatedButton.cs** | Control. `ButtonStyle` Primary/Ghost/Danger. Hover scale 1.015 / press 0.985 (eased). `IsLoading` spinner, focus ring. `Clicked` event (not `Click`). |
| **ModernTextBox.cs** | UserControl wrapping native TextBox, **vertically centered text** (positioning trick, no P/Invoke). Placeholder = solid `Label` overlay. Animated white focus / red error border + glow ring. `EnterKeyPressed` event, `SetError(bool)`. |
| **RoundedPanel.cs** | Rounded rect + optional multi-layer soft shadow + 1px hairline + top highlight. `Hoverable`. |
| **ScrollablePanel.cs** | Manual scrolling — `SetContentHeight(px)`, `ScrollByPixels(dy)` with clamping, cards store baseY in `Tag`, `RepositionChildren()` by `-offset`. Custom 6px proportional scrollbar. (AutoScroll deliberately abandoned — hidden scrollbars broke ranges.) |
| **ActivityLogCard.cs** | Left status color bar (widens on hover), tool name, status badge (PENDING/SUCCESS/ERROR), timestamp, 90-char input preview, model name. Routes wheel events to owning panel. |
| **StatCard.cs** | Top accent hairline, `SetCounter(int, suffix)` 800ms count-up, `Progress` 900ms animation, optional progress ring. |
| **StatusIndicator.cs** | Pulsing dot + pill. `ServerState` Active/Inactive/Connecting/Error; 40ms breathing pulse. |
| **ModelSelector.cs** | Standalone Linear-style dropdown trigger. **⚠️ UNUSED** — `DashboardForm` builds its selector inline. |
| **WheelDiag.cs** | Temporary diagnostics → `%LOCALAPPDATA%\Nexora\wheel_diag.log`. Production code still calls it; debug only, never throws. |

---

## 4. Data Flow

### Single binary, dual entry
```
NEXORA MCP.exe
   ├─ (no args) ──→ WinForms UI → Form1 (login)
   └─ --mcp ──────→ MCP stdio server (hosted, tools from assembly)
```

### Login (GUI)
1. `Form1.HandleLoginAsync` → validates fields
2. `HwidHelper.GetOrCreateDeviceId()` → HWID from `device.id`
3. `AuthService.LoginAsync(u, p, hwid)` → `POST /api/auth-login` → `LoginResult`
4. `TokenStore.SaveSession(...)` (memory) + `SaveToDisk()` → `session.json`
5. `Form1` hides → `DashboardForm` opens (FormClosed restores exit)

### Dashboard
- Loads models: `GET /api/ai-models` (Bearer) → dropdown; fallback = single Nemotron model
- Every 2.5s: `RefreshActivityLog()` reads `mcp_activity.json` (written by MCP process) →
  - `_serverActive = TokenStore.IsLoggedIn`
  - `_serverProcessing = any entry Status=="pending"`
  - tool-call count, success rate %, **estimated** latency (`min(2000, outputLen*3)`, avg of last 5)
  - renders last 15 tool-call cards; content-hash dedup prevents re-render
- 16ms timer animates server-card radial glow (ACTIVE green / PROCESSING amber)

### MCP tool call (the core loop)
1. OpenCode spawns `NEXORA MCP.exe --mcp` (stdio)
2. OpenCode calls `nexora_enhance_prompt(prompt, model)`
3. `ActivityLogger.LogPending(...)` → dashboard shows "PROCESSING"
4. If not logged in → `TokenStore.LoadFromDisk()` recovers GUI's persisted session
5. `NexoraAiClient.EnhancePromptAsync(...)` → `POST /api/ai` → enhanced prompt text
6. Text returned to OpenCode; `ActivityLogger.CompletePending(...)` marks done (dashboard shows SUCCESS/ERROR + output preview)

### Cross-process files (`%LOCALAPPDATA%\Nexora\`)
| File | Written by | Read by |
|---|---|---|
| `session.json` | GUI | MCP server (shared auth) |
| `mcp_activity.json` | MCP server (`ActivityLogger`) | GUI dashboard (polled 2.5s) |
| `device.id` | GUI (HwidHelper) | GUI/MCP (stable HWID) |
| `wheel_diag.log` | WheelDiag | debug only |

---

## 5. MCP Tool — `nexora_enhance_prompt`

- **Tool class**: `NexoraEnhancePromptTool` (`[McpServerToolType]`, static) in **Program.cs**
- **Params**: `prompt` (required), `model` (optional, default `nvidia/nemotron-3-ultra-550b-a55b`)
- **Behavior**: if session expired/missing → auto re-login via disk session; calls AI; logs activity; returns enhanced text (or user-facing error string, never throws)
- **Purpose**: transform short/vague requests → detailed execution-ready prompts. Never writes code. Uses `sysprompot.txt` as the system prompt (60+ domains, strict cybersecurity mode, output = plain prompt only, no preamble/markdown fences).

---

## 5b. EMBEDDED BROWSER FEATURE (`nexora_browser_*`)

**What it is**: A **WebView2 (Chromium) browser embedded INSIDE the app** — no separate Chrome window/process. The user sees/controls it via the dashboard **BROWSER** toggle; the AI agent (via MCP tools) drives the **same browser** through a loopback bridge. Login state persists across restarts.

### Architecture (two processes, one shared browser)
```
OpneCode ─stdio─> NEXORA MCP.exe --mcp (MCP server process, headless)
                        │  BrowserSession → BridgeClient
                        ▼  TCP 127.0.0.1:<random port>
    NEXORA MCP.exe (GUI) ── LoopbackBridgeServer
                        │  EmbeddedBrowserPanel (WebView2)  ← the REAL browser
                        └  user sees it via dashboard BROWSER toggle
```
- **GUI side** — `DashboardForm` hosts `EmbeddedBrowserPanel` (Dock=Fill, `Visible=true`, **SendToBack**; opaque `_dashContent` wraps the widgets). Browser keeps **rendering** even when hidden behind the dashboard → screenshots work while the agent works "behind the scenes". `ToggleBrowser()` = `BringToFront()` swap. Panel `EnsureInitializedAsync()` is **idempotent** (creates WebView2 core + starts `LoopbackBridgeServer` + writes `browser_status.json`) and runs on panel `Load` + eager from `Load` after models load. Bridge stops on `HandleDestroyed`.
- **MCP side** — `BrowserSession.cs` (singleton in the `--mcp` DI container), `BridgeClient.CallAsync<T>("op", args)` reads the port from `%LOCALAPPDATA%\Nexora\browser_status.json` (`bridgePort`, written on status-change). `SemaphoreSlim _gate` **serializes ALL ops**. No service/app closed → clear `BridgeClient.NeedAppMessage` error: **"Open the NEXORA MCP app…"** (the user must have the app running).
- **Calls are lazy/idempotent** — any `nexora_browser_*` tool ensures the bridge is up (`launch`). No browser server at MCP boot.
- **`NexoraBrowserTool.cs`** — `[McpServerToolType]` static class; every tool resolves `BrowserSession` via `IServiceProvider`, wraps in `ActivityLogger LogPending → CompletePending` so the dashboard feed shows browser activity. `ParseProxy` returns `(string Server, string? User, string? Pass)`.

### Bridge protocol
- TCP loopback, JSON: request `{ "op", "args" }` → response `{ "ok", "result", "error" }`, **4-byte LE length prefix** + UTF-8 body. Ops: `status, launch, navigate, get_content, screenshot, click, fill, type, press_key, evaluate, wait_for, get_tabs, switch_tab, close_tab, set_proxy, close`.
- **Responses use PascalCase record property names** (`IsRunning`, `ActivePageUrl`, `DataUrl`, …) and results are **JSON objects, not strings** — client must deserialize to the records (never `CallAsync<string>` for object ops).
- `ScreenshotResult.DataUrl` **includes the `data:image/png;base64,` prefix** — strip before `Convert.FromBase64String`.
- `launch` no-WebView2 → returns `IsRunning:false` + `NeedAppMessage` text.

### MCP Tools (all return JSON)
| Tool | Params | Purpose |
|---|---|---|
| `nexora_browser_status` | — | Running state, active page URL/title, tab count, proxy, profile path |
| `nexora_browser_launch` | `proxy?` (URL str) | Ensure embedded browser is up. Idempotent. |
| `nexora_browser_navigate` | `url`, `waitUntil` (load/domcontentloaded/commit/networkidle), `timeoutMs` | Go to URL; returns final URL/title/load time |
| `nexora_browser_get_content` | `format` (text/html), `selector?` (CSS), `maxChars` | Extract page text/**article content (for reading/review)** or HTML |
| `nexora_browser_screenshot` | `fullPage` | PNG → base64 data URL + saved file path in `%LOCALAPPDATA%\Nexora\Screenshots\` |
| `nexora_browser_click` | `selector` (CSS), `button` | Click button/link/checkbox |
| `nexora_browser_fill` | `selector`, `value` | Fill input/textarea (clears first) — forms, search, Gmail To/Subject |
| `nexora_browser_type` | `selector`, `text`, `delayMs` | Character-by-character typing (contenteditable, Gmail body) |
| `nexora_browser_press_key` | `key`, `selector?` | Keys/shortcuts: Enter, Tab, Escape, Control+A, etc. |
| `nexora_browser_wait_for` | `state` (networkidle/load), `timeoutMs` | Wait after AJAX (e.g., after Gmail Send) |
| `nexora_browser_evaluate` | `script` (JS), `argJson?` | Arbitrary JS in page → scraped/derived result |
| `nexora_browser_get_tabs` | — | All open tabs (index/url/title/active) — REAL multi-tab (one WebView2 per tab, shared profile); popups/`target=_blank` auto-become tabs |
| `nexora_browser_open_tab` | `url?`, `waitUntil`, `timeoutMs` | Open a new tab (URL or Google search query) and switch to it; returns new TabInfo |
| `nexora_browser_switch_tab` | `index` | Activate a tab for subsequent actions |
| `nexora_browser_close_tab` | `index?` | Close tab (default active); last tab resets to blank instead |
| `nexora_browser_click_at` | `x`, `y` (screenshot px), `button`, `clicks` | VIRTUAL CURSOR click at exact screenshot pixels (left/right/middle, 1-3 clicks). Loop: screenshot → read x/y → click_at. Re-screenshot after scroll/nav |
| `nexora_browser_hover_at` | `x`, `y` (screenshot px) | VIRTUAL CURSOR hover (tooltips, menus, hover states) |
| `nexora_browser_scroll` | `direction`, `pixels`, `x?`, `y?`, `selector?` | Scroll page or element; returns new scroll position |
| `nexora_browser_drag` | `x1`,`y1`,`x2`,`y2` (screenshot px), `steps` | Drag (sliders, maps, drag-drop, canvas). DUAL PATH: auto-detects HTML5 native DnD (`draggable=true`/React — CDP mouse can never trigger it) → JS DragEvent sequence (dragstart + dragenter/dragover along path + drop + dragend, DataTransfer with dragged text); otherwise human-paced CDP drag (60ms settle + 25ms/move, defeats input coalescing for path widgets like jQuery sortable) |
| `nexora_browser_go_back` / `go_forward` / `reload` | `timeoutMs` | History navigation (like toolbar buttons, for the agent) |
| `nexora_browser_set_viewport` | `width`, `height`, `scale?` | Responsive/rendering testing via CDP emulation (0/0 = reset); returns actual size. NOTE: `deviceScaleFactor` is REQUIRED by CDP — keep live DPR unless overriding |
| `nexora_browser_console` | — | Buffered console.log/warn/error + pageerrors since agent activity began on the page, then drains. Covers agent-triggered logs; parse-time (pre-interaction) logs NOT captured (WebView2 skips document-created scripts) |
| `nexora_browser_upload` | `selector`, `filePath` | File upload into `<input type=file>` via CDP `DOM.setFileInputFiles` (no dialog) |
| `nexora_browser_select_option` | `selector`, `value?`, `label?` | Dropdown select (fires input+change) |
| `nexora_browser_snapshot` | `maxNodes` | Compact element map: `[i] role "text" <css-path>` per interactive element (cheaper than screenshot for finding selectors) |
| `nexora_browser_set_proxy` | `server`, `username?`, `password?` | Session-level proxy; restarts WebView2, keeps profile. Empty server = disable |
| `nexora_browser_close` | — | Close/reset the embedded browser view (app keeps running) |

### Search engine & omnibox (preinstalled Google)
- Start page = `https://www.google.com` (navigated on first core init only, when source is blank; never clobbers a live page).
- `NormalizeAddress` (shared by toolbar `NavigateFromAddress` **and** bridge `DoNavigateAsync`, so the MCP agent gets it too): full URL → as-is; `data:`/`about:`/`blob:`/etc. scheme prefix → as-is (else WebView2 throws E_INVALIDARG); host-like (`example.com`, `localhost:5000`) → `https://` prefix; anything else → `https://www.google.com/search?q=<escaped>`.
- Address-bar sync timer (600 ms) updates `_address.Text` **only when the bar is NOT focused** (`!_address.Focused`) — otherwise it stomps user input back to the current URL (was the "typed text resets to about:blank" bug).

### Ultra pack — virtual cursor, multi-tab, testing (added 2026-09-10, 29 browser tools total)
- **Virtual cursor**: `DoScreenshotAsync` stores bitmap + CSS viewport metrics (`ScreenshotResult` now carries `WidthPx, HeightPx, ViewportW, ViewportH`); `MapShotToCssAsync` converts agent screenshot pixels → page coords, REJECTS stale coords when the viewport changed since the shot (agent must re-screenshot). Ops: `click_at` (buttons + multi-click), `hover_at`, `drag`, `scroll`.
- **Multi-tab engine**: `_tabs: List<Tab>` (one WebView2 each, shared UserDataFolder/profile); `_webView` is now a read-only property → active tab, so all existing ops route to it untouched. Only the active tab is `Visible` (hidden WebView2 stops rendering — screenshots/clicks always hit the visible one). Shared `CoreWebView2Environment` cached per proxy config (`_sharedEnv`/`_envProxy`); `NewWindowRequested` (popups/`target=_blank`) → real new tab via deferral. `ApplyProxyAsync` rebuilds ALL tabs. Status file + `BuildLaunchResultAsync` report real `tabsCount`.
- **Console capture**: `ConsoleHookJs` (buffer `__nxCon`, override + `__nxEnsure`, error/unhandledrejection listeners) installed via `AddScriptToExecuteOnDocumentCreatedAsync` (best effort) + `EnsureConsoleHookAsync()` before every interaction/read op. LESSONS (hard-won, do not regress): (1) WebView2 `ExecuteScriptAsync` **swallows JS errors as null-success** — always parse-check hook JS with `new Function` in E2E; actual bugs found this way: `try;` instead of `try{`, and an extra `)` in the error listener (both killed the whole hook silently). (2) CDP `Emulation.setDeviceMetricsOverride` REQUIRES `deviceScaleFactor` (omit → E_INVALIDARG). (3) `CoreWebView2.Source` is empty for `data:` URLs → `ResolveUrlAsync()` falls back to `document.URL`.
- `EnsureInitializedAsync` runs once via `_initTask` guard (Load + dashboard both call it; prevents double bridge/timer/home-nav).

### KeyMap & CDP shortcuts
- `EmbeddedBrowserPanel.KeyMap`: key/char → CDP VK code (modifier bitmask Ctrl=2, Alt=1, Shift=8, Meta=4). `KeyMap.Get` returns `Entry` directly (`.Key/.Code/.Vk/.Text`) — **not** a KeyValuePair (`.Value`).
- Click: `Input.dispatchMouseEvent` @ clicked-element coords; type/fill: `Input.insertText`; keys: `Input.dispatchKeyEvent`; screenshots: `Page.captureScreenshot` via CDP, fallback `CapturePreviewAsync`; JS: `ExecuteScriptAsync`.
- `GetTitleAsync` = `UnwrapJsString(document.title)` with `CoreWebView2.DocumentTitle` fallback. `ResetToBlankAsync` returns **`Task<bool>`**.
- WebView2 has **no `DisposeAsync`** in WinForms — use `Dispose()` (proxy restart path disposes + recreates the control, same UserDataFolder → logins persist).

### Proxy notes (gotchas)
- Format: `http://host:port`, `socks5://host:port`, creds inline `http://user:pass@host:port` (parsed by `ParseProxy` in `NexoraBrowserTool`) OR separate `username`/`password` params, OR at launch.
- Proxy applies at WebView2 **environment creation** (`CoreWebView2EnvironmentOptions(null, null, "--proxy-server=… --proxy-bypass-list=<-loopback>")`); proxy auth via `AddWebResourceRequestedFilter("*", …)` + `WebResourceRequested` → `Proxy-Authorization: Basic`.
- Runtime change = `ApplyProxyAsync`: dispose + recreate WebView2 (same UserDataFolder). ⚠️ **Never `Controls.RemoveAt(0)` when recreating — `Dispose()` already removes the old control from `Controls`, index-based removal breaks on the 2nd proxy change** ("Index 0 is out of range"). Only `Controls.Add(_webView); _webView.BringToFront();`.
- Changing proxy **closes open tabs** (env restart) — acceptable "session reset"; cookies/logins survive.

### Dashboard integration
- **BROWSER** button in the top bar toggles dashboard ↔ browser (z-order swap so WebView2 keeps compositing = screenshots always work).
- Browser actions appear in the activity feed (uses `ActivityLogger.LogPending/CompletePending` with `Tool="nexora_browser_*"`), so the status card shows them on the 2.5s poll.
- Optional future: a `browser_status.json`-driven status card (not yet built).

### Tested (2026-09-10)
- Bridge unit E2E (temp probe, compiles `BrowserBridge.cs`): **10/10 PASS** (port discovery, status/navigate/get_tabs roundtrips, error op throws, offline port null, offline throws NeedAppMessage, 20 concurrent calls).
- Live bridge E2E vs the real embedded browser: status, navigate example.com (title "Example Domain", ~250 ms), get_content, get_tabs, evaluate, screenshot (19,532-byte PNG saved) — PASS.
- Interaction E2E on a `data:text/html` page: navigate → fill → press_key Tab → type → click → wait_for; `document.title` became `CLICKED:Hello NexoraX` — PASS.
- Proxy E2E: set_proxy ON (`Restarted=true`), navigate after proxy, set_proxy OFF (after RemoveAt(0) fix) — PASS.
- **MCP-client E2E via official SDK** (`ModelContextProtocol` client, stdio, real `--mcp`): `tools/list` → all 16 `nexora_browser_*` + `nexora_enhance_prompt` listed; `nexora_browser_status` returned live bridge `{"IsRunning":true,…,ActivePageUrl":"about:blank","TabsCount":1}`; then `navigate example.com` (Title "Example Domain", 253 ms), `get_content` (245 chars, PASS), status URL updated — **full OpenCode→MCP→bridge→embedded-browser chain PASSED**.

---

## 6. Technology Stack

| Layer | Tech |
|---|---|
| Language | C# — records, pattern matching, switch expressions, nullable+implicit usings on |
| Runtime | .NET 8, `net8.0-windows`, WinForms (`UseWindowsForms`) |
| MCP | `ModelContextProtocol` 2.2.0 (stdlib transport, `AddMcpServer()`, `[McpServerTool]`) |
| Browser | `Microsoft.Web.WebView2` 1.0.4191.47 — **embedded** WebView2, driven by MCP via loopback bridge (NO external Chromium/Playwright) |
| Hosting/DI | `Microsoft.Extensions.Hosting` 10.0.11 |
| HTTP/JSON | `HttpClient` + `System.Net.Http.Json` + `System.Text.Json` |
| Native interop | `user32.dll` (`ReleaseCapture`, `SendMessage`) for dragging; `WM_MOUSEWHEEL` via `WndProc` + `IMessageFilter` |
| Backend | Vercel Node API `https://nexora-navy-omega.vercel.app` |
| Default AI model | `nvidia/nemotron-3-ultra-550b-a55b` (NVIDIA NIM, server-side key) |
| External client | OpenCode (config `~/.config/opencode/opencode.json` / `.jsonc`), project GUID `{C25A3EC1-DC31-4B7F-A3C5-2A28B3F7893C}` |

---

## 7. Configuration & Paths

- **No .env / appsettings / secrets in source.** All paths via `Environment.SpecialFolder`:

| Item | Path |
|---|---|
| Device ID | `%LOCALAPPDATA%\Nexora\device.id` |
| Session/token | `%LOCALAPPDATA%\Nexora\session.json` (plaintext JSON — convenience trade-off) |
| MCP activity log | `%LOCALAPPDATA%\Nexora\mcp_activity.json` |
| Wheel diagnostics | `%LOCALAPPDATA%\Nexora\wheel_diag.log` |
| **Browser profile** | `%LOCALAPPDATA%\Nexora\BrowserProfile\` (WebView2 user-data folder — cookies/logins) |
| **Browser screenshots** | `%LOCALAPPDATA%\Nexora\Screenshots\` |
| **Browser status** | `%LOCALAPPDATA%\Nexora\browser_status.json` (GUI writes `bridgePort` + state; MCP reads it) |
| WebView2 runtime | Evergreen installed on Win 10/11 (152.0.4191.66 on this machine); no bundling needed |
| OpenCode config | `~/.config/opencode/opencode.json` (or `.jsonc`) |
| API base URL | Constant in `TokenStore.BaseUrl` + `NexoraAiClient.BaseUrl` |

- **API endpoints**: auth-login (POST), ai (POST, Bearer, JSON/SSE), ai-models (GET, Bearer)
- **Test credentials**: `test` / `test123` (HWID auto-generated per machine)
- **Rate limits** (from API docs): 30 AI calls/min, 10 logins/5 min

---

## 8. Build & Run

```powershell
# Build
dotnet build "NEXORA MCP.sln" -c Debug

# Run GUI
dotnet run --project "NEXORA MCP\NEXORA MCP.csproj"

# Run MCP server mode
dotnet run --project "NEXORA MCP\NEXORA MCP.csproj" -- --mcp
# (or if already built):  & "NEXORA MCP\bin\Debug\net8.0-windows\NEXORA MCP.exe" --mcp
```

Requires **.NET 8 SDK on Windows** + installed **WebView2 runtime** (evergreen, ships with Win 11 / app drivers). Zero warnings build (nullable clean). One benign MSB3277 warning may appear (WindowsBase 4.0 vs 5.0 from WebView2's WPF lib) — SDK-level, ignorable, no CS warnings.

---

## 9. Conventions & Gotchas (IMPORTANT)

1. **Single WinExe, two modes** via `--mcp` flag. Never create a separate console project for the MCP server.
2. **Programmatic UI**: all controls `OnPaint` + `GraphicsPath`, `DoubleBuffered`, `UserPaint|AllPaintingInWmPaint|OptimizedDoubleBuffer`. No designer drag-drop.
3. **Theme centralization**: every control takes colors/fonts from `UITheme`. Don't hardcode hex in controls.
4. **Shared animation**: use `AnimationManager` (single timer). Easing fns in UITheme.
5. **Async/await + CancellationToken** for all I/O. `OperationCanceledException` handled in AuthService.
6. **Fail-silent resilience**: every disk/network op is try/catch that degrades gracefully.
7. **Activity lifecycle**: `LogPending("pending")` → `CompletePending("done")`. Dashboard derives server state from `pending` presence — not real MCP health.
8. **Custom scrolling only** — `ScrollablePanel` manual offset repositioning; native AutoScroll avoided on purpose. Wheel handled at 3 layers (control WndProc, form WndProc, global `IMessageFilter`).
9. **MCP tool convention**: `[McpServerToolType]` static class + `[McpServerTool(Name..., Description...)`], DI-injected `IServiceProvider`, services via `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`.
10. **Bridge concurrency**: `BrowserSession._gate` (SemaphoreSlim) serializes EVERY bridge op; bridge ops target the GUI's WebView2, so **the GUI app must be running** — else `BridgeClient.NeedAppMessage` ("Open the NEXORA MCP app…").
11. **Browser lazy/keep-alive**: browser (WebView2) is **always alive** once the dashboard opens (panel `EnsureInitializedAsync`) so agent screenshots work even when the dashboard view is showing; `nexora_browser_*` ops are lazy-ensure + idempotent. Profile never auto-deleted.
12. **Proxy = WebView2 env restart** (dispose + recreate control, same UserDataFolder → tabs reset, logins kept). ⚠️ Recreating: **never `Controls.RemoveAt` — `Dispose()` already removes the control from `Controls`; just `Controls.Add(_webView); _webView.BringToFront();`**.
13. **WebView2 / CDP notes**: bridge replies are flattened JSON objects (records, PascalCase) — map to the `BrowserSession` records, NOT raw strings. Screenshot `DataUrl` carries the `data:image/png;base64,` prefix. Bridge protocol = 4-byte LE length + UTF-8 JSON. WinForms WebView2 has no `DisposeAsync` (use `Dispose()`). `KeyMap.Get` returns `KeyMap.Entry` (`mapped.Key`, NOT `.Value`). Browser panel must stay `Visible=true` (SendToBack under `_dashContent`) so WebView2 keeps compositing → screenshots never cancel.
14. **Style**: flat dark "Linear/Vercel" — pure black base, 1px hairlines, white focus accents, hover micro-interactions, glow/pulse, custom scrollbars.
15. **Session stored plaintext on disk** — accepted trade-off for cross-process auth sharing.
16. **Latency is a heuristic estimate**, not measured.
17. ⚠️ `ModelSelector.cs` is **unused dead code** (dashboard builds selector inline) — safe to reference or delete.
18. ⚠️ `WheelDiag.cs` is **temporary debug** — remove production calls when wheel-scroll is confirmed stable.

---

## 10. API Response Shapes

```json
// Login success (200)
{ "ok": true, "token": "eyJ...", "user": { "username": "test", "expiry": 1796260386784, ... } }

// Login error (401)
{ "error": "Invalid username or password" }

// AI response (JSON mode)
{ "ok": true, "content": "Enhanced prompt text...", "finishReason": "stop" }

// Models list
{ "models": [ { "id": "...", "key": "...", "name": "...", "enabled": true, "maxTokens": 16384, "order": 1 } ] }
```

---

## 11. Known TODOs / Cleanup Candidates

- [ ] Remove production calls to `WheelDiag.Log` when scroll fix is confirmed
- [ ] Decide fate of unused `ModelSelector.cs`
- [ ] Remove legacy `NexoraMcp/` folder fallback path in `NexoraAiClient.LoadSystemPrompt`
- [ ] Optionally: move session token out of plaintext (secure persistence)
- [ ] Dashboard `BrowserStatusCard` reading `browser_status.json` (feature works without it; dashboard shows browser activity via activity feed)
- [ ] Delete leftover Playwright artifacts (`bin\Debug\net8.0-windows\.playwright\`, `Microsoft.Playwright.dll` in output) — stale from the old build, unused now

---

*Rebuilt & verified against actual source on 2026-09-10. Browser feature migrated from external Playwright/Chromium → **embedded WebView2 driven via loopback bridge**, full MCP→bridge→browser chain E2E-verified. Previous version (2026-09-05) documented a stale `NexoraMcp/` console project that no longer exists.*