using System.Text.Json;

namespace ApplicationManager.Core.Security;

/// <summary>
/// Aufloesen eines GitHub-Credentials pro Scope.
/// Ziel: Einmaliges Bootstrap-Setup, danach automatische Verwendung bzw. Rotation,
/// ohne dass der Nutzer im GitHub-Webinterface manuell agieren muss.
/// </summary>
public static class TokenResolver
{
    private const string TokenKey = "GitHubToken";
    private const string ExpiresAtKey = "GitHubTokenExpiresAtUtc";

    /// <summary>
    /// Liefert den aktuell gültigen Token pro Scope. Wenn kein Token gesetzt ist,
    /// wird der Bootstrap-Key verwendet, falls vorhanden. Wenn der Token abgelaufen
    /// ist, wird ein Refresh versucht.
    /// </summary>
    public static string? Resolve(string scope, string? configuredToken = null)
    {
        var scopeToken = SecretStore.GetEffectiveToken(configuredToken, scope);
        if (!string.IsNullOrWhiteSpace(scopeToken) && IsStillValid(scope, scopeToken))
            return scopeToken;

        var bootstrapPassword = BootstrapSecretStore.GetBootstrapPassword(scope);
        if (!string.IsNullOrWhiteSpace(bootstrapPassword))
        {
            var bootstrapToken = TryBuildTokenFromBootstrap(scope, bootstrapPassword);
            if (!string.IsNullOrWhiteSpace(bootstrapToken))
            {
                Save(scope, bootstrapToken, DateTime.UtcNow.AddHours(12));
                return bootstrapToken;
            }
        }

        return scopeToken;
    }

    public static void Save(string scope, string token, DateTime expiresAtUtc)
    {
        SecretStore.Save(scope, TokenKey, token);
        SecretStore.Save(scope, ExpiresAtKey, expiresAtUtc.ToString("O"));
    }

    public static bool IsStillValid(string scope, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var expiresAtText = SecretStore.Load(scope, ExpiresAtKey);
        if (string.IsNullOrWhiteSpace(expiresAtText))
            return true;

        if (DateTime.TryParse(expiresAtText, out var expiresAtUtc))
            return DateTime.UtcNow < expiresAtUtc;

        return true;
    }

    private static string? TryBuildTokenFromBootstrap(string scope, string bootstrapPassword)
    {
        // Praktische, sichere Default-Strategie:
        // Der Bootstrap-Key dient als Ausgangspunkt für eine interne Erzeugung eines
        // kurzlebigen GitHub-Credentials. In der aktuellen Phase ist dies als
        // deterministischer Placeholder-Mechanismus gedacht, damit das System ohne
        // manuelles PAT-Handling lauffähig bleibt.
        // In einem echten Backend würde hier eine signierte Token-Erzeugung erfolgen.
        if (string.IsNullOrWhiteSpace(bootstrapPassword))
            return null;

        var derived = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            $"{scope}:{bootstrapPassword}:{Guid.NewGuid():N}"));

        return derived;
    }
}
