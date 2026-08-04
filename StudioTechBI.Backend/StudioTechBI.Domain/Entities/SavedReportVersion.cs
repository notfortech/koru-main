namespace StudioTechBI.Domain.Entities;

/// <summary>
/// Immutable snapshot of one explicit "Save Report" click. Blob path points to Azure Blob Storage
/// (or local filesystem fallback) — never binary/HTML content stored in SQL.
/// </summary>
public class SavedReportVersion : BaseEntity
{
    public Guid SavedReportId { get; set; }
    public SavedReport SavedReport { get; set; } = null!;

    public int VersionNumber { get; set; }

    /// <summary>Relative blob path for the fully-assembled report HTML.</summary>
    public string? HtmlBlobPath { get; set; }

    /// <summary>The Report Generator computation-recipe template (kpis/charts), if any.</summary>
    public string? TemplateId { get; set; }
    public string? TemplateName { get; set; }

    /// <summary>The HTML chrome template this version was rendered against.</summary>
    public string? HtmlTemplateId { get; set; }
    public string? HtmlTemplateName { get; set; }

    public string? SourceFileName { get; set; }

    /// <summary>Small — inlined JSON of the filters applied when this version was generated.</summary>
    public string? AppliedFiltersJson { get; set; }

    public DateTime GeneratedDate { get; set; }

    /// <summary>Only one version per SavedReport is active (the one shown by default) at a time.</summary>
    public bool IsActive { get; set; }
}
