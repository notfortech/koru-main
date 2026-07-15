using StudioTechBI.Application.DTOs.ReportDesigner;

namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// Matches a client's extracted schema against the Approved SchemaModel directory and persists
/// the result as a ReportMatchDraft. Deterministic only for now — see ReportMatchResultDto.
/// </summary>
public interface IReportMatchService
{
    Task<ReportMatchResultDto> MatchAsync(Guid clientId, ExtractedSchemaDto schema, CancellationToken cancellationToken = default);
}
