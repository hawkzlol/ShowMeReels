using System.IO;

namespace ShowMeReels.App.Services;

internal static class AppDiagnostics
{
    private static readonly object SyncLock = new();

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ShowMeReels",
        "diagnostics.log");

    public static string CurrentLogPath => LogPath;

    public static void Log(string message)
    {
        try
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
            string? directory = Path.GetDirectoryName(LogPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            lock (SyncLock)
            {
                File.AppendAllText(LogPath, line);
            }
        }
        catch
        {
        }
    }
}
