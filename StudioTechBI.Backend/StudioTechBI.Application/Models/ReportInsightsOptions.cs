namespace StudioTechBI.Application.Models;

/// <summary>Server-side Power BI dataset sampling for report AI insights (ExecuteQueries).</summary>
public sealed class ReportInsightsOptions
{
    public const string SectionName = "ReportInsights";

    /// <summary>When true, call Power BI REST executeQueries after resolving workspace/dataset for the client asset.</summary>
    public bool DatasetExecuteQueriesEnabled { get; set; }

    /// <summary>Max rows returned per DAX query or per discovered table.</summary>
    public int MaxRowsPerQuery { get; set; } = 30;

    /// <summary>When no <see cref="Profiles"/> match, sample up to this many physical tables (in list order).</summary>
    public int MaxTablesToSample { get; set; } = 2;

    /// <summary>Stop after this many rows total across all samples (hard cap for LLM + API cost).</summary>
    public int MaxTotalSampleRows { get; set; } = 120;

    /// <summary>If no profile matches, list dataset tables and run TOPN on the first allowed tables.</summary>
    public bool AutoDiscoverTables { get; set; } = true;

    /// <summary>Skip tables whose name contains any of these (case-insensitive).</summary>
    public List<string> ExcludedTableNameFragments { get; set; } = new()
    {
        "DateTableTemplate",
        "LocalDateTable",
    };

    /// <summary>Optional tighter DAX for known report types/pages (recommended for production).</summary>
    public List<ReportInsightsDaxProfile> Profiles { get; set; } = new();
}

/// <summary>Runs only when report type matches and active page contains <see cref="PageNameContains"/> (case-insensitive), or substring is empty (any page).</summary>
public sealed class ReportInsightsDaxProfile
{
    public string? ReportType { get; set; }

    /// <summary>If set, matched when <c>ActivePageName</c> contains this substring.</summary>
    public string? PageNameContains { get; set; }

    public List<ReportInsightsNamedDax> Queries { get; set; } = new();
}

public sealed class ReportInsightsNamedDax
{
    /// <summary>Short label attached to sample (e.g. audit_summary).</summary>
    public string Label { get; set; } = "";

    /// <summary>Single DAX query returning a table (EVALUATE ...).</summary>
    public string Query { get; set; } = "";
}
