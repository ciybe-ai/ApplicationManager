namespace ApplicationManager.Core.Models;

/// <summary>
/// Format der zentral gehosteten Update-Manifest-Datei (JSON), z. B.:
/// {
///   "version": "1.2.0",
///   "systemAgentUrl": "https://intranet.example.com/it/ApplicationManager.SystemAgent.exe",
///   "systemAgentSha256": "AB12...",
///   "userAgentUrl": "https://intranet.example.com/it/ApplicationManager.UserAgent.exe",
///   "userAgentSha256": "CD34..."
/// }
/// </summary>
public class UpdateManifest
{
    public string Version { get; set; } = "0.0.0";
    public string? SystemAgentUrl { get; set; }
    public string? SystemAgentSha256 { get; set; }
    public string? UserAgentUrl { get; set; }
    public string? UserAgentSha256 { get; set; }
}
