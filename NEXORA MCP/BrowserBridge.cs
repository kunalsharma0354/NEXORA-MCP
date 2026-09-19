using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace NEXORA_MCP
{
    // ════════════════════════════════════════════════════════════════════════
    //  LOOPBACK BRIDGE — GUI process (hosts the embedded WebView2 browser)
    //  <→> MCP server process (drives the SAME browser via the browser tools).
    //
    //  A tiny TCP server binds ONLY to 127.0.0.1 on a random free port. The
    //  port is published in browser_status.json so the MCP client can find it.
    //  Protocol: 4-byte little-endian length prefix + UTF-8 JSON.
    //    request : { "op": "...", "args": {...} }
    //    response: { "ok": bool, "result": {...}|null, "error": string|null }
    // ════════════════════════════════════════════════════════════════════════

    public sealed class LoopbackBridgeServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Func<string, Task<string>> _handler;
        private readonly CancellationTokenSource _cts = new();
        private Task? _acceptLoop;
        private readonly object _sync = new();
        private bool _disposed;

        public int Port { get; }

        public LoopbackBridgeServer(Func<string, Task<string>> handler)
        {
            _handler = handler;
            _listener = new TcpListener(IPAddress.Loopback, 0); // random free port
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _acceptLoop = AcceptLoopAsync();
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await _listener.AcceptTcpClientAsync(); }
                catch { break; }
                _ = Task.Run(() => HandleAsync(client));
            }
        }

        private async Task HandleAsync(TcpClient client)
        {
            string response;
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = 60000;
                    var request = await ReadFrameAsync(stream).ConfigureAwait(false);
                    response = await _handler(request).ConfigureAwait(false);
                    var body = Encoding.UTF8.GetBytes(response);
                    var len = BitConverter.GetBytes(body.Length);
                    await stream.WriteAsync(len, 0, 4).ConfigureAwait(false);
                    await stream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                response = BridgeResponses.Error("bridge:" + ex.Message);
                try
                {
                    var body = Encoding.UTF8.GetBytes(response);
                    byte[] framed = new byte[4 + body.Length];
                    Array.Copy(BitConverter.GetBytes(body.Length), framed, 4);
                    Array.Copy(body, 0, framed, 4, body.Length);
                    await client.GetStream().WriteAsync(framed, 0, framed.Length).ConfigureAwait(false);
                }
                catch { }
            }
        }

        private static async Task<string> ReadFrameAsync(NetworkStream stream)
        {
            var lenBuf = new byte[4];
            await ReadExactAsync(stream, lenBuf, 4).ConfigureAwait(false);
            int length = BitConverter.ToInt32(lenBuf, 0);
            if (length < 0 || length > 32 * 1024 * 1024) throw new InvalidOperationException("Bridge frame too large.");
            var body = new byte[length];
            await ReadExactAsync(stream, body, length).ConfigureAwait(false);
            return Encoding.UTF8.GetString(body);
        }

        private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int n = await stream.ReadAsync(buffer, offset, count - offset).ConfigureAwait(false);
                if (n <= 0) throw new EndOfStreamException("Bridge connection closed.");
                offset += n;
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
            }
            _cts.Cancel();
            try { _listener.Stop(); } catch { }
        }
    }

    internal static class BridgeResponses
    {
        public static string Ok(object? result)
        {
            return JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["ok"] = true,
                ["result"] = result,
                ["error"] = null
            });
        }

        public static string Error(string message)
        {
            return JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["ok"] = false,
                ["result"] = null,
                ["error"] = message
            });
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    //  MCP-SIDE CLIENT — connects to the GUI's embedded browser bridge.
    // ────────────────────────────────────────────────────────────────────────

    public sealed class BridgeClient
    {
        public const string StatusFile = "browser_status.json";

        private readonly string _statusPath;

        public BridgeClient(string statusPath)
        {
            _statusPath = statusPath;
        }

        public static BridgeClient FromNexoraRoot()
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nexora");
            return new BridgeClient(Path.Combine(root, StatusFile));
        }

        public int? TryGetPort()
        {
            try
            {
                if (!File.Exists(_statusPath)) return null;
                using var doc = JsonDocument.Parse(File.ReadAllText(_statusPath));
                var root = doc.RootElement;
                if (root.TryGetProperty("isRunning", out var run) && run.GetBoolean() == false) return null;
                if (root.TryGetProperty("bridgePort", out var port)) return port.GetInt32();
                return null;
            }
            catch { return null; }
        }

        public static string NeedAppMessage =>
            "The embedded browser is not running. Open the NEXORA MCP app (start NEXORA MCP, sign in) so the " +
            "browser panel is available — the AI drives the exact same browser you see inside the app.";

        public async Task<T> CallAsync<T>(string op, object? args, CancellationToken ct = default)
        {
            var port = TryGetPort();
            if (port == null || port <= 0) throw new InvalidOperationException(NeedAppMessage);

            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(85));
            try
            {
                await client.ConnectAsync(IPAddress.Loopback, port.Value, cts.Token).ConfigureAwait(false);
            }
            catch
            {
                throw new InvalidOperationException($"{NeedAppMessage} (bridge connect refused on port {port}).");
            }

            using var stream = client.GetStream();
            var request = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["op"] = op,
                ["args"] = args ?? new Dictionary<string, object?>()
            });
            var reqBody = Encoding.UTF8.GetBytes(request);
            await stream.WriteAsync(BitConverter.GetBytes(reqBody.Length), 0, 4, cts.Token).ConfigureAwait(false);
            await stream.WriteAsync(reqBody, 0, reqBody.Length, cts.Token).ConfigureAwait(false);
            await stream.FlushAsync(cts.Token).ConfigureAwait(false);

            var lenBuf = new byte[4];
            int got = 0;
            while (got < 4)
            {
                int n = await stream.ReadAsync(lenBuf, got, 4 - got, cts.Token).ConfigureAwait(false);
                if (n <= 0) throw new InvalidOperationException(NeedAppMessage + " (bridge read failed).");
                got += n;
            }
            int length = BitConverter.ToInt32(lenBuf, 0);
            if (length < 0 || length > 32 * 1024 * 1024) throw new InvalidOperationException("Bridge response too large.");
            var body = new byte[length];
            got = 0;
            while (got < length)
            {
                int n = await stream.ReadAsync(body, got, length - got, cts.Token).ConfigureAwait(false);
                if (n <= 0) throw new InvalidOperationException(NeedAppMessage + " (bridge response truncated).");
                got += n;
            }

            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(body));
            var root = doc.RootElement;
            if (root.TryGetProperty("ok", out var okEl) && okEl.GetBoolean() == false)
            {
                var err = root.TryGetProperty("error", out var errEl) ? errEl.GetString() ?? "unknown error" : "unknown error";
                throw new InvalidOperationException(err);
            }
            if (root.TryGetProperty("result", out var resultEl))
            {
                if (resultEl.ValueKind == JsonValueKind.Null && typeof(T) != typeof(object)) return default!;
                return JsonSerializer.Deserialize<T>(resultEl.GetRawText())!;
            }
            throw new InvalidOperationException("Bridge returned no result.");
        }
    }
}