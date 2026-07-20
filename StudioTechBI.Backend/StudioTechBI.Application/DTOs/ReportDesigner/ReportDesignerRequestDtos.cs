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

/// <summary>
/// AiProvider lets the caller pick which LLM stbi_transformers should use for this
/// generation: "anthropic" (Claude Sonnet) or "openai". Null leaves stbi_transformers on
/// its own default. Only report-model generation supports this choice today — Blueprint
/// generation (the legacy AgentHost pipeline) is OpenAI-only and has no equivalent switch.
/// </summary>
public record GenerateReportModelRequest(
    string ClientId,
    ExtractedSchemaDto Schema,
    string? PreferredTheme,
    string? AiProvider = null);

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
/// persisted server-side, so it's sent back here rather than referenced by id).
///
/// TemplateId is optional and, when supplied, must be a Template the frontend already showed
/// the client as a match (via /report-designer/match) with IsPublishReady true — the frontend
/// is the source of truth for "which match is this," not a second server-side match call here.
/// When present and valid, publish rebinds that existing template's dataset to the client
/// instead of authoring a brand-new one. When absent (or the template isn't publish-ready),
/// publish falls back to the original chain: S7 TMDL authoring -> its deterministic validator
/// -> S8 dataset deploy. Same consent gate as generate-model either way — publishing is still
/// downstream of the same AI call that produced the blueprint in the first place.
/// </summary>
public record PublishReportRequest(
    string ClientId,
    JsonElement Blueprint,
    string? DatasetName = null,
    Guid? TemplateId = null);
