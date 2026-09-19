namespace NEXORA_MCP
{
    /// <summary>
    /// Provides a per-machine persistent unique device ID (HWID).
    /// Generated once and stored in LocalAppData so the machine keeps
    /// a stable identity across app restarts.
    /// </summary>
    public static class HwidHelper
    {
        private static string? _cached;

        public static string GetOrCreateDeviceId()
        {
            if (_cached is not null) return _cached;

            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Nexora");

                var file = Path.Combine(dir, "device.id");

                if (File.Exists(file))
                {
                    var existing = File.ReadAllText(file).Trim();
                    if (existing.Length > 0 && existing.Length <= 128)
                    {
                        _cached = existing;
                        return _cached;
                    }
                }

                Directory.CreateDirectory(dir);
                var id = Guid.NewGuid().ToString("N");
                File.WriteAllText(file, id);
                _cached = id;
                return _cached;
            }
            catch
            {
                // Fallback: still give a stable-ish id for this process lifetime.
                _cached ??= Guid.NewGuid().ToString("N");
                return _cached;
            }
        }
    }
}