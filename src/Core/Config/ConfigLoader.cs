using System.Net.Http.Headers;
using System.Text.Json;
using ApplicationManager.Core.Models;
using ApplicationManager.Core.Security;

namespace ApplicationManager.Core.Config;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static AppSettingsModel LoadSettings(string basePath, string scope = "user")
    {
        var settings = new AppSettingsModel();
        var mainPath = Path.Combine(basePath, "appsettings.json");
        var localPath = Path.Combine(basePath, "appsettings.Local.json");

        if (File.Exists(mainPath))
            settings = JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(mainPath), JsonOptions) ?? settings;

        if (string.IsNullOrWhiteSpace(settings.GitHubToken))
            settings.GitHubToken = TokenResolver.Resolve(scope, null);

        // appsettings.Local.json erlaubt maschinenspezifische Überschreibungen,
        // ohne die zentral verteilte appsettings.json anfassen zu müssen.
        if (File.Exists(localPath))
        {
            var overrideValues = JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(localPath), JsonOptions);
            if (overrideValues != null)
            {
                if (!string.IsNullOrWhiteSpace(overrideValues.GitHubToken)) settings.GitHubToken = overrideValues.GitHubToken;
                if (!string.IsNullOrWhiteSpace(overrideValues.BlacklistSource)) settings.BlacklistSource = overrideValues.BlacklistSource;
                if (!string.IsNullOrWhiteSpace(overrideValues.WishlistSource)) settings.WishlistSource = overrideValues.WishlistSource;
                if (!string.IsNullOrWhiteSpace(overrideValues.UpdateManifestUrl)) settings.UpdateManifestUrl = overrideValues.UpdateManifestUrl;
            }
        }

        settings.GitHubToken = TokenResolver.Resolve(scope, settings.GitHubToken);
        return settings;
    }

    public static async Task<List<BlacklistEntry>> LoadBlacklistAsync(string source, HttpClient http, string? accessToken = null)
    {
        var json = await ReadSourceAsync(source, http, accessToken);
        if (json == null) return new List<BlacklistEntry>();
        return JsonSerializer.Deserialize<List<BlacklistEntry>>(json, JsonOptions) ?? new List<BlacklistEntry>();
    }

    public static async Task<List<WishlistEntry>> LoadWishlistAsync(string source, HttpClient http, string? accessToken = null)
    {
        var json = await ReadSourceAsync(source, http, accessToken);
        if (json == null) return new List<WishlistEntry>();
        return JsonSerializer.Deserialize<List<WishlistEntry>>(json, JsonOptions) ?? new List<WishlistEntry>();
    }

    /// <summary>
    /// Liest eine Quelle wahlweise per HTTP(S) oder als lokalen/UNC-Dateipfad ein.
    /// So funktioniert dieselbe Konfiguration sowohl domänengebunden (UNC-Pfad
    /// auf einen Netlogon-Share) als auch domänenunabhängig übers Internet.
    /// Für private GitHub-Repo-Quellen kann ein PAT per Authorization-Header mitgegeben werden.
    /// </summary>
    private static async Task<string?> ReadSourceAsync(string source, HttpClient http, string? accessToken = null)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;

        if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, source);

                if (IsGitHubApiUrl(source) && !string.IsNullOrWhiteSpace(accessToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                }

                using var response = await http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                return null;
            }
        }

        return File.Exists(source) ? await File.ReadAllTextAsync(source) : null;
    }

    private static bool IsGitHubApiUrl(string source)
    {
        return source.Contains("api.github.com/repos/", StringComparison.OrdinalIgnoreCase)
            || source.Contains("api.github.com/contents/", StringComparison.OrdinalIgnoreCase);
    }
}
