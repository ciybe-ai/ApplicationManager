using ApplicationManager.Core.Agent;
using ApplicationManager.Core.Inventory;

// UserAgent laeuft im Kontext des angemeldeten Nutzers, ausgeloest durch
// eine geplante Aufgabe mit Trigger "Bei Anmeldung". Der gesamte Ablauf ist
// identisch zum SystemAgent und lebt gemeinsam in AgentRunner (Core) - hier
// wird nur festgelegt, WORAN sich UserAgent vom SystemAgent unterscheidet:
// Registry-Quelle (HKCU statt HKLM), winget-Scope und Pfade.

await AgentRunner.RunAsync(new AgentOptions
{
    AgentName = "useragent",
    Assembly = typeof(Program).Assembly,
    GetInstalledApps = AppInventory.GetCurrentUserApps,
    WingetScope = "user",
    CleanDefaultProfile = false,
    IsSystemAgent = false,
    DefaultDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ApplicationManager")
});
