namespace ApplicationManager.Core.Security;

/// <summary>
/// Hilft beim ersten Setup: Ein einmalig eingegebenes Bootstrap-Passwort wird pro
/// Scope separat gespeichert. Damit kann ein externer Token-Provider oder ein
/// internes Backend später automatisch passende GitHub-Credentials erzeugen,
/// ohne dass der Benutzer im GitHub-Webinterface Arbeiten ausführen muss.
/// </summary>
public static class BootstrapSecretStore
{
    private const string BootstrapPasswordKey = "BootstrapPassword";

    public static void SaveBootstrapPassword(string scope, string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Bootstrap-Passwort darf nicht leer sein.", nameof(password));

        SecretStore.Save(scope, BootstrapPasswordKey, password);
    }

    public static string? GetBootstrapPassword(string scope)
    {
        return SecretStore.Load(scope, BootstrapPasswordKey);
    }

    public static string? ResolveBootstrapToken(string scope, string? configuredToken = null)
    {
        return SecretStore.GetEffectiveToken(configuredToken, scope);
    }
}
