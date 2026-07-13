using StudioTechBI.Application.DTOs.ReportDesigner;

namespace StudioTechBI.Application.Interfaces;

public interface IReportDesignerClient
{
    Task<GenerateReportModelResponse> GenerateReportModelAsync(
        GenerateReportModelRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// AI-assisted schema/model matching, used when deterministic name-overlap scoring
    /// against the SchemaModel directory falls below its confidence gate. Headers/types
    /// only — see SchemaModelAiMatchRequest.
    /// </summary>
    Task<SchemaModelAiMatchResponse> MatchSchemaModelAsync(
        SchemaModelAiMatchRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}
