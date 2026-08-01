using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ApplicationManager.Core.Security;

/// <summary>
/// Speichert sensible Tokens pro Scope separat und verschlüsselt auf der Maschine
/// bzw. im aktuellen Benutzerkontext. Dadurch können SystemAgent und UserAgent
/// ihre eigenen Credentials unabhängig verwalten, auch wenn beide dieselbe
/// GitHub-Quelle nutzen.
/// </summary>
public static class SecretStore
{
    private const string SecretFileName = "secrets.json";

    public static string? GetEffectiveToken(string? configuredToken, string scope)
    {
        if (!string.IsNullOrWhiteSpace(configuredToken))
            return configuredToken;

        var scopedEnvName = scope.Equals("system", StringComparison.OrdinalIgnoreCase)
            ? "APPLICATIONMANAGER_SYSTEM_GITHUB_TOKEN"
            : "APPLICATIONMANAGER_USER_GITHUB_TOKEN";

        var value = Environment.GetEnvironmentVariable(scopedEnvName)
            ?? Environment.GetEnvironmentVariable("APPLICATIONMANAGER_GITHUB_TOKEN");

        if (!string.IsNullOrWhiteSpace(value))
            return value;

        return Load(scope, "GitHubToken");
    }

    public static void Save(string scope, string key, string value)
    {
        var path = GetSecretsPath(scope);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var dictionary = LoadDictionary(path);
        dictionary[key] = Convert.ToBase64String(
            ProtectedData.Protect(
                Encoding.UTF8.GetBytes(value),
                null,
                scope.Equals("system", StringComparison.OrdinalIgnoreCase)
                    ? DataProtectionScope.LocalMachine
                    : DataProtectionScope.CurrentUser));

        File.WriteAllText(path, JsonSerializer.Serialize(dictionary));
    }

    public static string? Load(string scope, string key)
    {
        var path = GetSecretsPath(scope);
        if (!File.Exists(path))
            return null;

        var dictionary = LoadDictionary(path);
        if (!dictionary.TryGetValue(key, out var encryptedValue) || string.IsNullOrWhiteSpace(encryptedValue))
            return null;

        try
        {
            var bytes = ProtectedData.Unprotect(
                Convert.FromBase64String(encryptedValue),
                null,
                scope.Equals("system", StringComparison.OrdinalIgnoreCase)
                    ? DataProtectionScope.LocalMachine
                    : DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static string GetSecretsPath(string scope)
    {
        var baseDirectory = scope.Equals("system", StringComparison.OrdinalIgnoreCase)
            ? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(baseDirectory, "ApplicationManager", SecretFileName);
    }

    private static Dictionary<string, string> LoadDictionary(string path)
    {
        if (!File.Exists(path))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = File.ReadAllText(path);
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return values ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
