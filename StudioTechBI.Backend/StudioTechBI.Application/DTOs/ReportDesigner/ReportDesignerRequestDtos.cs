using System.Text.Json;

namespace StudioTechBI.Application.DTOs.ReportDesigner;

public record SqlConnectionRequest(
    string Host,
    int Port,
    string Database,
    string Username,
    string Password);

public record SharePointBrowseRequest(string SiteUrl);

public record SharePointExtractRequest(
    string SiteUrl,
    string DriveItemId,
    string FileName);

public record GenerateReportModelRequest(
    string ClientId,
    ExtractedSchemaDto Schema,
    string? PreferredTheme);

/// <summary>
/// Records (or declines) a client's consent to send schema metadata for a specific
/// schema shape to the Report Designer AI. Must be called, with ConsentGranted = true,
/// before GenerateReportModelRequest will be accepted for that (ClientId, SchemaHash) pair.
/// </summary>
public record ReportDesignerConsentRequest(
    string ClientId,
    string SchemaHash,
    bool ConsentGranted);

/// <summary>
/// S9 — the "Generate & Publish" capstone. Blueprint is the raw JSON from an already-successful
/// GenerateReportModelResponse.Blueprint (the frontend holds it client-side; it was never
/// persisted server-side, so it's sent back here rather than referenced by id). Chains: S7
/// TMDL authoring -> its deterministic validator -> S8 dataset deploy. Same consent gate as
/// generate-model — publishing is still downstream of the same AI call that produced the
/// blueprint in the first place.
/// </summary>
public record PublishReportRequest(
    string ClientId,
    JsonElement Blueprint,
    string? DatasetName = null);
