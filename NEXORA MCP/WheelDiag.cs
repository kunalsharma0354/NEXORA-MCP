namespace NEXORA_MCP
{
    /// <summary>
    /// Temporary file-based diagnostics for the activity-log wheel scrolling.
    /// Writes every wheel event the app sees (and scroll attempts) to
    /// %LOCALAPPDATA%\Nexora\wheel_diag.log so we can prove what reaches the app.
    /// </summary>
    internal static class WheelDiag
    {
        private static readonly object Gate = new();
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nexora", "wheel_diag.log");

        public static void Log(string msg)
        {
            try
            {
                lock (Gate)
                {
                    File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {msg}{Environment.NewLine}");
                }
            }
            catch
            {
                // diagnostics must never break the app
            }
        }
    }
}