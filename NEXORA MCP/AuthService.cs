using System.Net.Http.Json;
using System.Text.Json;

namespace NEXORA_MCP
{
    /// <summary>Raised when authentication fails with a user-friendly message.</summary>
    public class AuthException : Exception
    {
        public AuthException(string message, int? httpStatus = null) : base(message)
        {
            HttpStatus = httpStatus;
        }

        public int? HttpStatus { get; }
    }

    public sealed record LoginResult(string Token, string Username, DateTime Expiry);

    /// <summary>
    /// Talks to the real NEXORA auth endpoint:
    /// POST {BaseUrl}/api/auth-login  →  { ok, token, user }
    /// </summary>
    public static class AuthService
    {
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        public static async Task<LoginResult> LoginAsync(string username, string password, string hwid, CancellationToken ct = default)
        {
            username = username.Trim();

            if (string.IsNullOrWhiteSpace(username))
                throw new AuthException("Please enter your username.");
            if (string.IsNullOrWhiteSpace(password))
                throw new AuthException("Please enter your password.");
            if (string.IsNullOrWhiteSpace(hwid))
                throw new AuthException("Device ID is unavailable.");

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{TokenStore.BaseUrl}/api/auth-login")
            {
                Content = JsonContent.Create(new
                {
                    username,
                    password,
                    hwid
                })
            };
            request.Headers.Add("Accept", "application/json");

            HttpResponseMessage response;
            try
            {
                response = await Http.SendAsync(request, ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new AuthException("The server is taking too long to respond. Try again.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new AuthException("Network error. Check your internet connection and try again.");
            }

            using (response)
            {
                string raw = await response.Content.ReadAsStringAsync(ct);
                return HandleResponse(response, raw);
            }
        }

        private static LoginResult HandleResponse(HttpResponseMessage response, string raw)
        {
            string httpError = TryExtractError(raw);

            switch ((int)response.StatusCode)
            {
                case 200:
                {
                    using var doc = JsonDocument.Parse(raw);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("token", out var tokenEl) ||
                        string.IsNullOrWhiteSpace(tokenEl.GetString()))
                    {
                        throw new AuthException("Login response was invalid. Please try again.");
                    }

                    var token = tokenEl.GetString()!;
                    var name = root.TryGetProperty("user", out var userEl) && userEl.TryGetProperty("username", out var nameEl)
                        ? nameEl.GetString() ?? string.Empty
                        : string.Empty;

                    DateTime? expiry = null;
                    if (root.TryGetProperty("user", out var user) && user.TryGetProperty("expiry", out var expEl))
                    {
                        var expiryRaw = expEl.ValueKind == JsonValueKind.Number ? expEl.GetInt64() : (long)expEl.GetDouble();
                        expiry = DateTimeOffset.FromUnixTimeMilliseconds(expiryRaw).UtcDateTime;
                    }

                    return new LoginResult(token, name, expiry ?? DateTime.UtcNow.AddDays(30));
                }

                case 400:
                    throw new AuthException(string.IsNullOrEmpty(httpError) ? "Invalid login request." : httpError, (int)response.StatusCode);
                case 401:
                    throw new AuthException("Invalid username or password.", 401);
                case 403:
                    throw new AuthException(string.IsNullOrEmpty(httpError)
                        ? "Your account is blocked or this device is not allowed."
                        : httpError, 403);
                case 429:
                {
                    var wait = response.Headers.RetryAfter?.Delta;
                    var baseMsg = "Too many login attempts. Please wait a minute and try again.";
                    var msg = wait.HasValue
                        ? $"Too many login attempts. Try again in {Math.Max(1, (int)wait.Value.TotalSeconds)}s."
                        : baseMsg;
                    throw new AuthException(msg, 429);
                }
                default:
                    throw new AuthException(string.IsNullOrEmpty(httpError)
                        ? "Something went wrong on the server. Try again later."
                        : httpError, (int)response.StatusCode);
            }
        }

        private static string TryExtractError(string raw)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("error", out var err))
                    return err.GetString() ?? string.Empty;
            }
            catch (JsonException)
            {
                // Non-JSON body — ignore, fall back to defaults.
            }
            return string.Empty;
        }
    }
}