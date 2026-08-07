namespace StudioTechBI.Application.DTOs.ReportDesigner;

/// <summary>
/// Poll response for an async "Data Model" generation job. Schema is echoed back from the
/// original request (never persisted separately client-side) so the frontend can reconstruct its
/// wizard state -- e.g. after the user navigated away and clicks a "ready" notification -- without
/// re-uploading or re-extracting the source file. Result is populated only once Status is
/// "Completed".
/// </summary>
public class ReportModelGenerationJobDto
{
    public Guid GenerationId { get; set; }

    /// <summary>Correlation ID matching the RequestId used for the AI call.</summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>Pending | Processing | Completed | Failed</summary>
    public string Status { get; set; } = string.Empty;

    public ExtractedSchemaDto? Schema { get; set; }
    public GenerateReportModelResponse? Result { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
