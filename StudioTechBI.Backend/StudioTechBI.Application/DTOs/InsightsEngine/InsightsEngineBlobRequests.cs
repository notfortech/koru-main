namespace StudioTechBI.Application.DTOs.InsightsEngine;

/// <summary>POST body for report tabular sample (matches frontend Insights page).</summary>
public sealed class ReportDataSampleRequest
{
    public string? ClientCode { get; set; }
    public int MaxRows { get; set; } = 100;
    /// <summary>When true, use JWT <c>client_code</c> to resolve the client; <see cref="ClientCode"/> is optional/ignored for resolution.</summary>
    public bool UseSelectedClient { get; set; }
}

/// <summary>POST body to resolve the latest report blob (matches frontend).</summary>
public sealed class ResolveBlobRequest
{
    public string? ClientCode { get; set; }
    public bool UseSelectedClient { get; set; }
}
