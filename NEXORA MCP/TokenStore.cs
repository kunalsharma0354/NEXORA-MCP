using System.Text.Json;

namespace NEXORA_MCP
{
    /// <summary>
    /// Holds the authenticated session state for the whole app runtime.
    /// The bearer token is persisted to a user-scoped file so that the
    /// MCP server process (spawned separately by OpenCode) can share the session.
    /// </summary>
    public static class TokenStore
    {
        public const string BaseUrl = "https://nexora-navy-omega.vercel.app";

        private static readonly string SessionFilePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nexora", "session.json");

        private sealed class SessionData
        {
            public string Token { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string DeviceId { get; set; } = string.Empty;
            public DateTime IssuedAt { get; set; }
            public DateTime Expiry { get; set; }
        }

        public static string AccessToken { get; private set; } = string.Empty;
        public static string Username { get; private set; } = string.Empty;
        public static string DeviceId { get; private set; } = string.Empty;
        public static DateTime TokenIssuedAt { get; private set; } = DateTime.MinValue;
        public static DateTime TokenExpiry { get; private set; } = DateTime.MinValue;

        public static bool IsLoggedIn => !string.IsNullOrWhiteSpace(AccessToken);

        public static void SaveSession(string token, string username, string deviceId, DateTime? expiry, DateTime? issuedAt)
        {
            AccessToken = token ?? string.Empty;
            Username = username ?? string.Empty;
            DeviceId = deviceId ?? string.Empty;
            TokenIssuedAt = issuedAt ?? DateTime.UtcNow;
            TokenExpiry = expiry ?? TokenIssuedAt.AddDays(30);

            // If the expiry is not directly provided, try to decode it from the JWT payload.
            if (expiry == null && !string.IsNullOrEmpty(token) && TryDecodeExpiry(token, out var exp))
            {
                TokenExpiry = exp;
            }
        }

        public static void SaveToDisk()
        {
            try
            {
                if (!IsLoggedIn) return;

                var dir = Path.GetDirectoryName(SessionFilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                var data = new SessionData
                {
                    Token = AccessToken,
                    Username = Username,
                    DeviceId = DeviceId,
                    IssuedAt = TokenIssuedAt,
                    Expiry = TokenExpiry
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SessionFilePath, json);
            }
            catch (Exception)
            {
                // Never let a disk failure break the app.
            }
        }

        public static void LoadFromDisk()
        {
            try
            {
                if (!File.Exists(SessionFilePath)) return;
                var json = File.ReadAllText(SessionFilePath);
                var data = JsonSerializer.Deserialize<SessionData>(json);
                if (data is null || string.IsNullOrWhiteSpace(data.Token)) return;

                if (data.Expiry != default && data.Expiry < DateTime.UtcNow)
                {
                    DeleteFromDisk();
                    return;
                }

                SaveSession(data.Token, data.Username, data.DeviceId, data.Expiry, data.IssuedAt);
            }
            catch (Exception)
            {
                // Corrupt or unreadable session file — ignore.
            }
        }

        public static void DeleteFromDisk()
        {
            try
            {
                if (File.Exists(SessionFilePath)) File.Delete(SessionFilePath);
            }
            catch (Exception)
            {
                // Ignore.
            }
        }

        public static void ClearSession()
        {
            AccessToken = string.Empty;
            Username = string.Empty;
            DeviceId = string.Empty;
            TokenIssuedAt = DateTime.MinValue;
            TokenExpiry = DateTime.MinValue;
            DeleteFromDisk();
        }

        private static bool TryDecodeExpiry(string token, out DateTime expiry)
        {
            expiry = DateTime.MinValue;
            try
            {
                var parts = token.Split('.');
                if (parts.Length < 2) return false;

                var payload = parts[1]
                    .Replace('-', '+')
                    .Replace('_', '/');
                payload += new string('=', (4 - payload.Length % 4) % 4);

                using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
                if (doc.RootElement.TryGetProperty("exp", out var expEl) &&
                    expEl.TryGetInt64(out var expSeconds))
                {
                    expiry = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
                    return true;
                }
            }
            catch (Exception)
            {
                // Not a standard JWT payload — fall back to the safe default (30 days).
            }
            return false;
        }
    }
}