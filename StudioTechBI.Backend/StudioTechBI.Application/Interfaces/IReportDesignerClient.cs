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

    /// <summary>
    /// S7/S9 — sends an already-generated blueprint (the raw JSON from
    /// GenerateReportModelResponse.Blueprint) to stbi_transformers' TMDL-authoring agent, which
    /// also runs its deterministic validator before returning. Check
    /// AuthorTmdlResponse.Validation.IsValid before treating the files as deployable.
    /// </summary>
    Task<AuthorTmdlResponse> AuthorTmdlAsync(
        System.Text.Json.JsonElement blueprint,
        string correlationId,
        CancellationToken cancellationToken = default);
}
