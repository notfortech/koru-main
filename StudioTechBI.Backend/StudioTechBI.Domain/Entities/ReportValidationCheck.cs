namespace StudioTechBI.Domain.Entities;

/// <summary>
/// One check result within a ReportValidationRun. CheckFamily covers both the Phase 1 families
/// (RenderingHealth, DataSanity) and the Phase 2 ones (FilterCorrectness, ExportIntegrity) so
/// Phase 2 slots in without a schema change.
/// </summary>
public class ReportValidationCheck : BaseEntity
{
    public Guid ReportValidationRunId { get; set; }
    public ReportValidationRun Run { get; set; } = null!;

    public string CheckFamily { get; set; } = string.Empty;

    /// <summary>Short machine name, e.g. "no-blank-widgets", "zero-console-errors", "kpi-finite-values".</summary>
    public string CheckName { get; set; } = string.Empty;

    /// <summary>Pass | Warning | Fail</summary>
    public string Status { get; set; } = string.Empty;

    public string? Detail { get; set; }

    /// <summary>JSON blob for structured evidence (console error list, failed request list, the
    /// specific offending KPI/chart value) — rendered in the drill-down view.</summary>
    public string? EvidenceJson { get; set; }

    public int SortOrder { get; set; }
}

public static class ReportValidationCheckFamilies
{
    public const string RenderingHealth = "RenderingHealth";
    public const string DataSanity = "DataSanity";
    public const string FilterCorrectness = "FilterCorrectness"; // Phase 2
    public const string ExportIntegrity = "ExportIntegrity";     // Phase 2
}

public static class ReportValidationCheckStatuses
{
    public const string Pass = "Pass";
    public const string Warning = "Warning";
    public const string Fail = "Fail";
}
