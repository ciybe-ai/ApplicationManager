using ApplicationManager.Core.Agent;
using ApplicationManager.Core.Inventory;

// SystemAgent laeuft als SYSTEM, ausgeloest durch eine geplante Aufgabe mit
// Trigger "Bei Anmeldung". Der gesamte Ablauf ist identisch zum UserAgent
// und lebt gemeinsam in AgentRunner (Core) - hier wird nur festgelegt,
// WORAN sich SystemAgent von UserAgent unterscheidet: Registry-Quelle
// (HKLM statt HKCU), winget-Scope, Default-Profil-Bereinigung und Pfade.

await AgentRunner.RunAsync(new AgentOptions
{
    AgentName = "systemagent",
    Assembly = typeof(Program).Assembly,
    GetInstalledApps = AppInventory.GetMachineApps,
    WingetScope = "machine",
    CleanDefaultProfile = true,
    IsSystemAgent = true,
    DefaultDataDirectory = @"C:\ProgramData\ApplicationManager"
});
