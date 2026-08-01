namespace ApplicationManager.Core.Logging;

/// <summary>
/// Simpler, threadsicherer Datei-Logger. Bewusst ohne externe Logging-
/// Bibliothek gehalten, damit das Basisprogramm ohne zusätzliche NuGet-
/// Abhängigkeiten auskommt.
/// </summary>
public class FileLogger
{
    private readonly string _logFilePath;
    private readonly object _lock = new();

    public FileLogger(string dataDirectory, string fileName)
    {
        Directory.CreateDirectory(dataDirectory);
        _logFilePath = Path.Combine(dataDirectory, fileName);
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
        lock (_lock)
        {
            try
            {
                File.AppendAllLines(_logFilePath, new[] { line });
            }
            catch
            {
                // Logging darf den eigentlichen Betrieb nie zum Absturz bringen.
            }
        }
    }
}
