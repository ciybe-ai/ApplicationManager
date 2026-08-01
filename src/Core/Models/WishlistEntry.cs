namespace ApplicationManager.Core.Models;

/// <summary>
/// Ein gewünschtes Standardprogramm, das per winget installiert werden soll.
/// </summary>
public class WishlistEntry
{
    /// <summary>winget-Paket-ID, z. B. "7zip.7zip".</summary>
    public string PackageId { get; set; } = "";

    /// <summary>"machine" (Standard, per Service) oder "user" (per UserAgent).</summary>
    public string Scope { get; set; } = "machine";
}
