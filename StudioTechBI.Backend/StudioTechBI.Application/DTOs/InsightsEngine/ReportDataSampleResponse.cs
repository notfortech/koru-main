namespace StudioTechBI.Application.DTOs.InsightsEngine;

/// <summary>Tabular sample from the client’s latest report/accounting blob (CSV/XLSX), for UI preview.</summary>
public sealed class ReportDataSampleResponse
{
    public List<string> Columns { get; set; } = new();

    /// <summary>Row objects keyed by column name; values are typically strings from the file.</summary>
    public List<Dictionary<string, object?>> Rows { get; set; } = new();

    /// <summary>Number of data rows returned (after header).</summary>
    public int RowCount { get; set; }

    /// <summary>True when <see cref="RowCount"/> reached the requested max (file may have more rows).</summary>
    public bool Truncated { get; set; }
}
