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

        // Last-write stamp of session.json as we last saw it — lets a running MCP
        // server (spawned by OpenCode) pick up a fresh login/logout done by the app
        // WITHOUT any restart. OpenCode only ever calls this exe; everything
        // session-related lives here and on disk.
        private static DateTime _seenStampUtc = DateTime.MinValue;

        /// <summary>Reloads the in-memory session from disk when session.json has
        /// changed since we last read it (new login, logout, expiry repair...).
        /// Cheap: one FileInfo stat per call.</summary>
        public static void EnsureFresh()
        {
            try
            {
                var fi = new FileInfo(SessionFilePath);
                var stamp = fi.Exists ? fi.LastWriteTimeUtc : DateTime.MinValue;
                if (stamp == _seenStampUtc) return;

                InvalidateMemory();
                LoadFromDisk();
            }
            catch
            {
                // Disk hiccup — keep whatever memory state we have.
            }
        }

        public static void SaveSession(string token, string username, string deviceId, DateTime? expiry, DateTime? issuedAt)
        {
            AccessToken = token ?? string.Empty;
            Username = username ?? string.Empty;
            DeviceId = deviceId ?? string.Empty;
            TokenIssuedAt = issuedAt ?? DateTime.UtcNow;

            // Never keep a past expiry: a unit mismatch (seconds vs milliseconds)
            // would invalidate the session the moment it is reloaded from disk.
            var effective = expiry != null && expiry.Value > TokenIssuedAt && expiry.Value > DateTime.UtcNow ? expiry : null;

            if (effective != null)
            {
                TokenExpiry = effective.Value;
            }
            else if (TryDecodeExpiry(AccessToken, out var jwtExp) && jwtExp > DateTime.UtcNow)
            {
                TokenExpiry = jwtExp;
            }
            else
            {
                TokenExpiry = TokenIssuedAt.AddDays(30);
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
                _seenStampUtc = new FileInfo(SessionFilePath).LastWriteTimeUtc;
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
                var fi = new FileInfo(SessionFilePath);
                if (!fi.Exists)
                {
                    _seenStampUtc = DateTime.MinValue;
                    return;
                }
                _seenStampUtc = fi.LastWriteTimeUtc;

                var json = File.ReadAllText(SessionFilePath);
                var data = JsonSerializer.Deserialize<SessionData>(json);
                if (data is null || string.IsNullOrWhiteSpace(data.Token)) return;

                if (data.Expiry != default && data.Expiry < DateTime.UtcNow)
                {
                    // Self-heal for sessions written by older builds that stored a
                    // bogus expiry (seconds/milliseconds unit mismatch): if the token
                    // itself still carries a valid JWT exp, repair the file instead of
                    // throwing the login away.
                    if (TryDecodeExpiry(data.Token, out var jwtExp) && jwtExp > DateTime.UtcNow)
                    {
                        SaveSession(data.Token, data.Username, data.DeviceId, jwtExp, data.IssuedAt);
                        SaveToDisk();
                        return;
                    }

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
                _seenStampUtc = DateTime.MinValue;
            }
            catch (Exception)
            {
                // Ignore.
            }
        }

        /// <summary>Clears only the in-memory session, not the disk file. Used when a
        /// stored token turns out to be dead (server-side 401, e.g. after a key
        /// rotation): the next call reloads from disk, so a fresh app login is
        /// picked up immediately without restarting OpenCode.</summary>
        public static void InvalidateMemory()
        {
            AccessToken = string.Empty;
            Username = string.Empty;
            DeviceId = string.Empty;
            TokenIssuedAt = DateTime.MinValue;
            TokenExpiry = DateTime.MinValue;
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