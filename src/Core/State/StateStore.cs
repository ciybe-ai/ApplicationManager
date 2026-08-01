using System.Text.Json;

namespace ApplicationManager.Core.State;

public class RunState
{
    /// <summary>Format je Eintrag: "AppName|SID" (SID leer = maschinenweit).</summary>
    public HashSet<string> ProcessedUninstalls { get; set; } = new();

    /// <summary>Bereits installierte winget-Paket-IDs.</summary>
    public HashSet<string> ProcessedInstalls { get; set; } = new();

    /// <summary>Zuletzt geprüfte/übernommene Update-Version.</summary>
    public string LastKnownVersion { get; set; } = "0.0.0";

    public DateTime? LastUpdateCheckUtc { get; set; }
}

/// <summary>
/// Persistiert den Fortschritt lokal pro Maschine (Service) bzw. pro Nutzer
/// (UserAgent), damit bei jedem neuen Zyklus nicht wieder bei 0 begonnen wird.
/// </summary>
public class StateStore
{
    private readonly string _statePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public StateStore(string dataDirectory, string fileName)
    {
        Directory.CreateDirectory(dataDirectory);
        _statePath = Path.Combine(dataDirectory, fileName);
    }

    public RunState Load()
    {
        if (!File.Exists(_statePath)) return new RunState();
        try
        {
            var json = File.ReadAllText(_statePath);
            return JsonSerializer.Deserialize<RunState>(json) ?? new RunState();
        }
        catch
        {
            return new RunState();
        }
    }

    public void Save(RunState state)
    {
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(_statePath, json);
    }
}
