namespace StudioTechBI.Domain.Entities;

/// <summary>
/// Top-level aggregate for a client's "Saved Reports" list. Mirrors Blueprint/BlueprintVersion:
/// one SavedReport per named report, each explicit save produces a new SavedReportVersion.
/// A thin union over two source types (SavedReportSourceTypes) rather than two parallel entities —
/// GeneratedHtml reports are blob-backed via Versions; PowerBiRequestFulfilled reports have no
/// versions and instead point at an existing, untouched PowerBiAsset row.
/// </summary>
public class SavedReport : BaseEntity
{
    public Guid ClientId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SourceType { get; set; } = SavedReportSourceTypes.GeneratedHtml;

    /// <summary>Active or Archived.</summary>
    public string Status { get; set; } = "Active";

    /// <summary>Monotonically increasing counter — used as the version number on each SavedReportVersion.</summary>
    public int VersionCount { get; set; }

    /// <summary>Set only when SourceType == PowerBiRequestFulfilled.</summary>
    public Guid? PowerBiAssetId { get; set; }

    public ICollection<SavedReportVersion> Versions { get; set; } = new List<SavedReportVersion>();
}
