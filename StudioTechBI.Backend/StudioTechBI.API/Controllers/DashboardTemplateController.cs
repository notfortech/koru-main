using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.DTOs.BindDeploy;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.DashboardTemplate;
using StudioTechBI.Application.DTOs.ReportDesigner;
using StudioTechBI.Application.DTOs.TemplateRefresh;
using StudioTechBI.Application.Constants;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Application.Options;
using StudioTechBI.Application.Utilities;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.API.Controllers;

/// <summary>
/// Dashboard Template Generator — a wholly new, separate flow from Report Designer's publish
/// (ReportDesignerController.PublishAsync). Given a client's uploaded file and an
/// already-generated blueprint (from POST /api/report-designer/generate-model, or the richer
/// "Design Blueprint" format — see BlueprintFormatDetector), this:
///  1. Blends real values with clearly-labeled mock values for any columns the upload doesn't
///     cover (Phase 1 — DataBlendService).
///  2. Patches the authored TMDL's data source to a short-lived SAS URL (Phase 2 — TmdlSourcePatcher).
///  3. Generates a real report.json with visuals (Phase 3 — ReportVisualGenerator; genuinely
///     greenfield, see that class's remarks).
///  4. Assembles a PBIP file set and publishes it via stbi-bind-deploy's new Import API client
///     (Phase 4 — IPbipImportClient), writing a PowerBiAsset row on success so the existing
///     embed-token flow picks it up automatically.
/// The deploy step (3-4) fails soft: if it errors, the request still returns everything Phase 1-2
/// already produce (blended dataset + patched TMDL), with the failure appended to the same log —
/// no live Power BI tenant was available to verify this pipeline against in this session.
/// </summary>
[ApiController]
[Route("api/dashboard-template")]
[Authorize]
public class DashboardTemplateController : ControllerBase
{
    private static readonly string[] AllowedExtensions = { ".xlsx", ".xls" };
    private static readonly TimeSpan SasValidFor = TimeSpan.FromHours(1);

    /// <summary>Same "instant refresh" threshold TemplateRefreshService already uses for the
    /// standalone template-refresh flow — kept identical rather than inventing a second number.</summary>
    private const double MatchConfidenceThreshold = 0.8;

    private readonly IReportDesignerClient _reportDesignerClient;
    private readonly IDataBlendService _blendService;
    private readonly IWorkbookWriter _workbookWriter;
    private readonly IBlobStorageService _blobStorage;
    private readonly IBlobSasUriProvider _sasUriProvider;
    private readonly IReportVisualGenerator _reportVisualGenerator;
    private readonly IPbipImportClient _pbipImportClient;
    private readonly IPowerBiAssetWriter _powerBiAssetWriter;
    private readonly IDashboardTemplateLogWriter _logWriter;
    private readonly IClientResolver _clientResolver;
    private readonly IAiGateway _aiGateway;
    private readonly ITemplateMatchingService _templateMatching;
    private readonly ITemplateService _templateService;
    private readonly ITemplateRebindClient _templateRebindClient;
    private readonly IAuditLogService _auditLog;
    private readonly DashboardTemplateOptions _options;
    private readonly IOptionsMonitor<TemplateCatalogOptions> _catalogOptions;
    private readonly ILogger<DashboardTemplateController> _logger;
    private readonly IOptions<UploadLimitsOptions> _uploadLimits;

    public DashboardTemplateController(
        IReportDesignerClient reportDesignerClient,
        IDataBlendService blendService,
        IWorkbookWriter workbookWriter,
        IBlobStorageService blobStorage,
        IBlobSasUriProvider sasUriProvider,
        IReportVisualGenerator reportVisualGenerator,
        IPbipImportClient pbipImportClient,
        IPowerBiAssetWriter powerBiAssetWriter,
        IDashboardTemplateLogWriter logWriter,
        IClientResolver clientResolver,
        IAiGateway aiGateway,
        ITemplateMatchingService templateMatching,
        ITemplateService templateService,
        ITemplateRebindClient templateRebindClient,
        IAuditLogService auditLog,
        IOptions<DashboardTemplateOptions> options,
        IOptionsMonitor<TemplateCatalogOptions> catalogOptions,
        ILogger<DashboardTemplateController> logger,
        IOptions<UploadLimitsOptions> uploadLimits)
    {
        _reportDesignerClient = reportDesignerClient;
        _blendService = blendService;
        _workbookWriter = workbookWriter;
        _blobStorage = blobStorage;
        _sasUriProvider = sasUriProvider;
        _reportVisualGenerator = reportVisualGenerator;
        _pbipImportClient = pbipImportClient;
        _powerBiAssetWriter = powerBiAssetWriter;
        _logWriter = logWriter;
        _clientResolver = clientResolver;
        _aiGateway = aiGateway;
        _templateMatching = templateMatching;
        _templateService = templateService;
        _templateRebindClient = templateRebindClient;
        _auditLog = auditLog;
        _options = options.Value;
        _catalogOptions = catalogOptions;
        _logger = logger;
        _uploadLimits = uploadLimits;
    }

    /// <summary>Best-effort — a logging failure must never break the actual response.</summary>
    private async Task LogAttemptAsync(Guid clientId, string clientName, bool success, string summary, IReadOnlyList<string> logLines, CancellationToken cancellationToken)
    {
        try
        {
            await _logWriter.LogAsync(clientId, clientName, success, summary, logLines, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DashboardTemplate.LogWriteFailed ClientId={ClientId}", clientId);
        }
    }

    /// <summary>Best-effort — a logging failure must never break the actual response.</summary>
    private async Task LogBuildRequestAsync(Guid clientId, string clientName, string blobPath, IReadOnlyList<string> requestedColumns, string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            await _logWriter.LogBuildRequestAsync(clientId, clientName, blobPath, requestedColumns, correlationId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DashboardTemplate.LogBuildRequestFailed ClientId={ClientId}", clientId);
        }
    }

    /// <summary>Best-effort — a logging failure must never break the actual response.</summary>
    private async Task LogMatchCheckFailedAsync(Guid clientId, string clientName, string correlationId, Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            await _logWriter.LogMatchCheckFailedAsync(clientId, clientName, correlationId, exception, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DashboardTemplate.LogMatchCheckFailedFailed ClientId={ClientId}", clientId);
        }
    }

    private sealed record GenerationContext(
        Client Client,
        string CorrelationId,
        JsonElement Blueprint,
        string? DesignTemplateId,
        string? DesignTier,
        string? DesignLabel);

    private sealed record ContextResolution(GenerationContext? Context, IActionResult? Error);

    /// <summary>Shared input validation, blueprintId resolution, blueprint JSON parsing, and
    /// client lookup — identical for GenerateAsync and VerifyMatchAsync; only what happens with
    /// the resolved context differs between the two actions.</summary>
    private async Task<ContextResolution> ResolveGenerationContextAsync(
        IFormFile file,
        string clientId,
        string? blueprint,
        Guid? blueprintId,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return new ContextResolution(null, BadRequest(ApiResponse<object>.ErrorResponse("No file uploaded.")));

        if (file.Length > _uploadLimits.Value.MaxUploadBytes)
            return new ContextResolution(null, BadRequest(ApiResponse<object>.ErrorResponse(
                $"File exceeds the {_uploadLimits.Value.MaxUploadBytes / (1024 * 1024)} MB limit.")));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return new ContextResolution(null, BadRequest(ApiResponse<object>.ErrorResponse(
                $"Unsupported file type '{ext}'. Allowed: {string.Join(", ", AllowedExtensions)}")));

        if (string.IsNullOrWhiteSpace(clientId))
            return new ContextResolution(null, BadRequest(ApiResponse<object>.ErrorResponse("clientId is required.")));

        if (string.IsNullOrWhiteSpace(blueprint) && blueprintId is null)
            return new ContextResolution(null, BadRequest(ApiResponse<object>.ErrorResponse("Either blueprint or blueprintId is required.")));

        if (!string.IsNullOrWhiteSpace(blueprint) && blueprintId is not null)
            return new ContextResolution(null, BadRequest(ApiResponse<object>.ErrorResponse("Supply only one of blueprint or blueprintId, not both.")));

        if (blueprintId is not null)
        {
            blueprint = await _aiGateway.GetBlueprintJsonAsync(blueprintId.Value, cancellationToken);
            if (string.IsNullOrWhiteSpace(blueprint))
                return new ContextResolution(null, NotFound(ApiResponse<object>.ErrorResponse(
                    $"No generated JSON found for blueprint '{blueprintId}'. It may still be processing, or generation may have failed.")));
        }

        JsonElement blueprintElement;
        try
        {
            using var blueprintDoc = JsonDocument.Parse(blueprint!);
            // Cloned so the element outlives the using block above — JsonDocument's buffer is
            // pooled/disposed at scope exit, and this element is read well past that point.
            blueprintElement = blueprintDoc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return new ContextResolution(null, BadRequest(ApiResponse<object>.ErrorResponse("blueprint is not valid JSON.")));
        }

        var client = await _clientResolver.ResolveAsync(clientId, cancellationToken);
        if (client is null)
            return new ContextResolution(null, NotFound(ApiResponse<object>.ErrorResponse($"Client '{clientId}' not found.")));

        var isDesignBlueprint = BlueprintFormatDetector.IsDesignBlueprint(blueprintElement);
        var context = new GenerationContext(
            client,
            Guid.NewGuid().ToString(),
            blueprintElement,
            isDesignBlueprint ? ReadStringProperty(blueprintElement, "templateId") : null,
            isDesignBlueprint ? ReadStringProperty(blueprintElement, "tier") : null,
            isDesignBlueprint ? ReadStringProperty(blueprintElement, "label") : null);

        return new ContextResolution(context, null);
    }

    private sealed record MatchCheckResult(
        TemplateDto? Matched,
        double? Confidence,
        List<TemplateCandidateSummaryDto> Candidates);

    /// <summary>Checks the real template catalog for a match against this client's blended
    /// columns. Shared by GenerateAsync (clone-instead-of-generate-from-scratch) and
    /// VerifyMatchAsync (the cheap pre-check) so the exact same ranking/qualification logic
    /// backs both. A "qualifying" match is the first ranked candidate that is both
    /// IsPublishReady and at/above MatchConfidenceThreshold; every ranked candidate (qualifying
    /// or not) is also returned in full so callers can show the client what was considered.</summary>
    private async Task<MatchCheckResult> FindQualifyingMatchAsync(Client client, BlendResult blended, CancellationToken cancellationToken)
    {
        var flattenedColumns = blended.Tables
            .SelectMany(t => t.Columns)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var matchResponse = await _templateMatching.MatchFromColumnsAsync(
            client.ClientCode ?? client.Id.ToString(), useSelectedClient: true, flattenedColumns, useAiRefinement: true, cancellationToken);

        TemplateDto? matched = null;
        double? matchConfidence = null;
        var candidates = new List<TemplateCandidateSummaryDto>();

        foreach (var candidate in matchResponse.Templates)
        {
            var candidateTemplate = await _templateService.GetByIdAsync(candidate.TemplateId, cancellationToken);
            if (candidateTemplate is null)
                continue;

            candidates.Add(new TemplateCandidateSummaryDto(
                candidateTemplate.TemplateId, candidateTemplate.TemplateName, candidate.Confidence, candidateTemplate.IsPublishReady));

            if (matched is null && candidate.Confidence >= MatchConfidenceThreshold && candidateTemplate.IsPublishReady)
            {
                matched = candidateTemplate;
                matchConfidence = candidate.Confidence;
            }
        }

        return new MatchCheckResult(matched, matchConfidence, candidates);
    }

    /// <summary>
    /// POST /api/dashboard-template/generate
    /// Multipart form: file (the client's Excel upload), clientId, and either blueprint (raw
    /// JSON string — either the Analytics Blueprint from POST /api/report-designer/generate-model,
    /// or a Design Blueprint — auto-detected, see BlueprintFormatDetector) or blueprintId (a
    /// Blueprint Generator ID from POST /api/blueprints/generate — resolved here via the same
    /// IAiGateway.GetBlueprintJsonAsync the Blueprints screen's own PDF/JSON download already
    /// uses, so a generated blueprint can be turned into a dashboard template directly without
    /// the caller re-copying its JSON by hand). Exactly one of the two must be supplied.
    /// </summary>
    [HttpPost("generate")]
    [RequestSizeLimit(UploadLimits.MaxUploadBytes)]
    public async Task<IActionResult> GenerateAsync(
        IFormFile file,
        [FromForm] string clientId,
        [FromForm] string? blueprint,
        [FromForm] Guid? blueprintId,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveGenerationContextAsync(file, clientId, blueprint, blueprintId, cancellationToken);
        if (resolved.Error is not null)
            return resolved.Error;
        var ctx = resolved.Context!;
        var client = ctx.Client;
        var correlationId = ctx.CorrelationId;
        var blueprintElement = ctx.Blueprint;
        var designTemplateId = ctx.DesignTemplateId;
        var designTier = ctx.DesignTier;
        var designLabel = ctx.DesignLabel;

        AuthorTmdlResponse authored;
        try
        {
            authored = await _reportDesignerClient.AuthorTmdlAsync(blueprintElement, correlationId, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "DashboardTemplate.AuthorTmdlFailed ClientId={ClientId} CorrelationId={CorrelationId}", client.Id, correlationId);
            await LogAttemptAsync(client.Id, client.ClientName, false, "Failed to author the semantic model.", new[] { ex.Message }, cancellationToken);
            return StatusCode(502, ApiResponse<object>.ErrorResponse("Failed to author the semantic model. Please try again."));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "DashboardTemplate.AuthorTmdlFailed ClientId={ClientId} CorrelationId={CorrelationId}", client.Id, correlationId);
            await LogAttemptAsync(client.Id, client.ClientName, false, "Failed to author the semantic model.", new[] { ex.Message }, cancellationToken);
            return StatusCode(502, ApiResponse<object>.ErrorResponse("Failed to author the semantic model. Please try again."));
        }

        BlendResult blended;
        await using (var uploadStream = file.OpenReadStream())
        {
            try
            {
                blended = await _blendService.BlendAsync(uploadStream, blueprintElement, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "DashboardTemplate.BlendFailed ClientId={ClientId} CorrelationId={CorrelationId}", client.Id, correlationId);
                await LogAttemptAsync(client.Id, client.ClientName, false, "Could not read the uploaded file to blend with the generated model.", new[] { ex.Message }, cancellationToken);
                return UnprocessableEntity(ApiResponse<object>.ErrorResponse(
                    "Could not read the uploaded file to blend with the generated model.", new List<string> { ex.Message }));
            }
        }

        if (blended.Tables.Count == 0)
            return UnprocessableEntity(ApiResponse<object>.ErrorResponse(
                "The generated blueprint has no tables to build a dashboard template from."));

        await using var workbookStream = await _workbookWriter.WriteAsync(blended.Tables, cancellationToken);
        var blobPath = $"{client.Id}/dashboard-templates/{correlationId}/blended-data.xlsx";
        await _blobStorage.UploadClientBlobAsync(
            blobPath,
            workbookStream,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            cancellationToken);

        var downloadUrl = await _sasUriProvider.GetReadSasUriAsync(blobPath, SasValidFor, cancellationToken);

        var patchedFiles = TmdlSourcePatcher.PatchSourceFilePath(
            authored.Files, downloadUrl ?? blobPath, out var tmdlPatched);

        var uploadedCount = blended.Provenance.Count(p => p.Source == ProvenanceSource.Uploaded);
        var mockedCount = blended.Provenance.Count(p => p.Source == ProvenanceSource.Mocked);

        var deployed = false;
        string? workspaceId = null;
        string? datasetId = null;
        string? reportId = null;
        // "PendingTemplateBuild" (no qualifying catalog match — a build request was filed) or
        // "MatchCheckFailed" (the match check or the clone itself failed technically) unless a
        // qualifying match is found and cloned below, in which case source becomes "MatchedTemplate".
        // No longer falls through to generating a report from scratch below the match threshold —
        // ReportVisualGenerator/IPbipImportClient stay wired up but are unreachable from this path.
        var source = "PendingTemplateBuild";
        Guid? matchedTemplateId = null;
        string? matchedTemplateName = null;
        double? matchConfidence = null;
        var visualLog = new List<string>();

        if (!downloadUrl.HasSasScheme())
        {
            visualLog.Add("Deploy skipped: no SAS download URL was available for the blended dataset — Power BI would not be able to fetch it.");
        }
        else
        {
            MatchCheckResult? matchCheck = null;
            try
            {
                matchCheck = await FindQualifyingMatchAsync(client, blended, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "DashboardTemplate.MatchAttemptFailed ClientId={ClientId} CorrelationId={CorrelationId}", client.Id, correlationId);
                visualLog.Add("Template catalog match check failed. Please contact our support team for assistance.");
                await LogMatchCheckFailedAsync(client.Id, client.ClientName, correlationId, ex, cancellationToken);
                source = "MatchCheckFailed";
            }

            if (matchCheck?.Matched is not null)
            {
                var matchedTemplate = matchCheck.Matched;
                matchConfidence = matchCheck.Confidence;
                try
                {
                    visualLog.Add(
                        $"Matched existing published template '{matchedTemplate.TemplateName}' (confidence {matchConfidence:P0}) — cloning its dataset/report instead of generating from scratch.");

                    var rebindRequest = new TemplateRebindRequest(
                        ClientName: $"Client - {client.ClientName}",
                        TemplateWorkspaceName: _catalogOptions.CurrentValue.MasterWorkspaceName,
                        TemplateDatasetName: matchedTemplate.TemplateName,
                        SourceFilePath: downloadUrl ?? blobPath);

                    var rebindResult = await _templateRebindClient.RebindAsync(rebindRequest, correlationId, cancellationToken);
                    visualLog.AddRange(rebindResult.Steps);

                    deployed = true;
                    workspaceId = rebindResult.WorkspaceId;
                    datasetId = rebindResult.DatasetId;
                    reportId = rebindResult.ReportId;
                    source = "MatchedTemplate";
                    matchedTemplateId = matchedTemplate.TemplateId;
                    matchedTemplateName = matchedTemplate.TemplateName;

                    visualLog.Add(
                        "Not written to PowerBiAsset automatically for this path yet — map WorkspaceId/DatasetId/ReportId above into that table manually to make the report visible in the client's portal.");

                    await _auditLog.WriteAsync(new AuditLogEntryDto
                    {
                        TenantId = null,
                        Action = "DashboardTemplateMatchedAndCloned",
                        EntityType = "Template",
                        EntityId = matchedTemplate.TemplateId.ToString(),
                        NewValue = JsonSerializer.Serialize(new
                        {
                            ClientId = client.Id,
                            MatchConfidence = matchConfidence,
                            rebindResult.WorkspaceId,
                            rebindResult.DatasetId,
                            rebindResult.ReportId,
                            rebindResult.ReportCloned
                        })
                    }, cancellationToken);

                    _logger.LogInformation(
                        "DashboardTemplate.MatchedAndCloned ClientId={ClientId} CorrelationId={CorrelationId} TemplateId={TemplateId} WorkspaceId={WorkspaceId} DatasetId={DatasetId} ReportId={ReportId} ReportCloned={ReportCloned}",
                        client.Id, correlationId, matchedTemplate.TemplateId, workspaceId, datasetId, reportId, rebindResult.ReportCloned);
                }
                catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
                {
                    _logger.LogWarning(ex, "DashboardTemplate.CloneFailed ClientId={ClientId} CorrelationId={CorrelationId} TemplateId={TemplateId}",
                        client.Id, correlationId, matchedTemplate.TemplateId);
                    visualLog.Add("Cloning the matched template failed. Please contact our support team for assistance.");
                    source = "MatchCheckFailed";
                    await LogMatchCheckFailedAsync(client.Id, client.ClientName, correlationId, ex, cancellationToken);
                }
            }
            else if (source != "MatchCheckFailed")
            {
                // No qualifying match found, and the check itself ran cleanly — file a build
                // request instead of generating a report from scratch.
                visualLog.Add("No confident match found in the template catalog. Your template will be ready soon — our team has been notified to build one.");
                var requestedColumns = blended.Tables.SelectMany(t => t.Columns).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                await LogBuildRequestAsync(client.Id, client.ClientName, blobPath, requestedColumns, correlationId, cancellationToken);
            }
        }

        var summary = $"{uploadedCount} of {blended.Provenance.Count} columns used from your file; " +
                      $"{mockedCount} column(s) mocked — see the provenance log for which ones." +
                      (tmdlPatched ? "" : " Note: the authored semantic model's data source could not be auto-patched; wire SourceFilePath manually.") +
                      (deployed
                          ? source == "MatchedTemplate"
                              ? " Matched an existing published template and cloned it — map the returned IDs into PowerBiAsset manually to make it visible."
                              : " A live Power BI report was generated with visuals."
                          : " The Power BI report was not deployed — see the log for why.");

        _logger.LogInformation(
            "DashboardTemplate.Generated ClientId={ClientId} CorrelationId={CorrelationId} Uploaded={Uploaded} Mocked={Mocked} TmdlPatched={TmdlPatched} Deployed={Deployed}",
            client.Id, correlationId, uploadedCount, mockedCount, tmdlPatched, deployed);

        await LogAttemptAsync(client.Id, client.ClientName, deployed, summary, visualLog, cancellationToken);

        return Ok(ApiResponse<GenerateDashboardTemplateResponse>.SuccessResponse(
            new GenerateDashboardTemplateResponse(
                correlationId,
                blended.Provenance,
                blobPath,
                downloadUrl,
                patchedFiles,
                tmdlPatched,
                summary,
                deployed,
                workspaceId,
                datasetId,
                reportId,
                visualLog,
                designTemplateId,
                designTier,
                designLabel,
                source,
                matchedTemplateId,
                matchedTemplateName,
                matchConfidence),
            "Dashboard template generated."));
    }

    /// <summary>
    /// POST /api/dashboard-template/verify-match
    /// Cheap pre-check for the "Generate Dashboard Template" button: blends the client's file
    /// with the blueprint and checks the real template catalog for a qualifying match — skips
    /// TMDL authoring and every Power BI call entirely (those only matter for the deploy step),
    /// so this comes back fast. Same multipart contract as /generate (file, clientId,
    /// blueprint|blueprintId). A "Pending" result files a build request for staff; an "Error"
    /// result means the match check itself failed technically — logged for staff, generic
    /// "contact support" message to the caller.
    /// </summary>
    [HttpPost("verify-match")]
    [RequestSizeLimit(UploadLimits.MaxUploadBytes)]
    public async Task<IActionResult> VerifyMatchAsync(
        IFormFile file,
        [FromForm] string clientId,
        [FromForm] string? blueprint,
        [FromForm] Guid? blueprintId,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveGenerationContextAsync(file, clientId, blueprint, blueprintId, cancellationToken);
        if (resolved.Error is not null)
            return resolved.Error;
        var ctx = resolved.Context!;

        BlendResult blended;
        await using (var uploadStream = file.OpenReadStream())
        {
            try
            {
                blended = await _blendService.BlendAsync(uploadStream, ctx.Blueprint, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "DashboardTemplate.VerifyMatch.BlendFailed ClientId={ClientId} CorrelationId={CorrelationId}", ctx.Client.Id, ctx.CorrelationId);
                return UnprocessableEntity(ApiResponse<object>.ErrorResponse(
                    "Could not read the uploaded file to blend with the generated model.", new List<string> { ex.Message }));
            }
        }

        if (blended.Tables.Count == 0)
            return UnprocessableEntity(ApiResponse<object>.ErrorResponse(
                "The generated blueprint has no tables to check for a template match."));

        await using var workbookStream = await _workbookWriter.WriteAsync(blended.Tables, cancellationToken);
        var blobPath = $"{ctx.Client.Id}/dashboard-templates/{ctx.CorrelationId}/blended-data.xlsx";
        await _blobStorage.UploadClientBlobAsync(
            blobPath,
            workbookStream,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            cancellationToken);

        var downloadUrl = await _sasUriProvider.GetReadSasUriAsync(blobPath, SasValidFor, cancellationToken);

        MatchCheckResult matchCheck;
        try
        {
            matchCheck = await FindQualifyingMatchAsync(ctx.Client, blended, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "DashboardTemplate.VerifyMatch.MatchCheckFailed ClientId={ClientId} CorrelationId={CorrelationId}", ctx.Client.Id, ctx.CorrelationId);
            await LogMatchCheckFailedAsync(ctx.Client.Id, ctx.Client.ClientName, ctx.CorrelationId, ex, cancellationToken);

            return Ok(ApiResponse<VerifyTemplateMatchResponse>.SuccessResponse(
                new VerifyTemplateMatchResponse(
                    ctx.CorrelationId,
                    "Error",
                    "Please contact our support team for assistance.",
                    blended.Provenance,
                    blobPath,
                    downloadUrl,
                    null,
                    null,
                    null,
                    new List<TemplateCandidateSummaryDto>()),
                "Template match check failed."));
        }

        string status;
        string message;
        if (matchCheck.Matched is not null)
        {
            status = "Matched";
            message = $"Matched an existing published template '{matchCheck.Matched.TemplateName}' (confidence {matchCheck.Confidence:P0}).";
        }
        else
        {
            status = "Pending";
            message = "Your template will be ready soon.";
            var requestedColumns = blended.Tables.SelectMany(t => t.Columns).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            await LogBuildRequestAsync(ctx.Client.Id, ctx.Client.ClientName, blobPath, requestedColumns, ctx.CorrelationId, cancellationToken);
        }

        _logger.LogInformation(
            "DashboardTemplate.VerifyMatch ClientId={ClientId} CorrelationId={CorrelationId} Status={Status} MatchedTemplateId={MatchedTemplateId}",
            ctx.Client.Id, ctx.CorrelationId, status, matchCheck.Matched?.TemplateId);

        return Ok(ApiResponse<VerifyTemplateMatchResponse>.SuccessResponse(
            new VerifyTemplateMatchResponse(
                ctx.CorrelationId,
                status,
                message,
                blended.Provenance,
                blobPath,
                downloadUrl,
                matchCheck.Matched?.TemplateId,
                matchCheck.Matched?.TemplateName,
                matchCheck.Confidence,
                matchCheck.Candidates),
            message));
    }

    private static string? ReadStringProperty(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>Re-roots authored TMDL paths under "{slug}.SemanticModel/definition/..." regardless
    /// of AuthorTmdlAsync's raw path convention — if the path already contains a "definition/"
    /// segment, everything from there onward is preserved; otherwise the whole relative path is
    /// kept as-is under the canonical prefix.</summary>
    private static string ToSemanticModelPath(string slug, string rawPath)
    {
        var normalized = rawPath.Replace('\\', '/').TrimStart('/');
        const string marker = "definition/";
        var idx = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        var relative = idx >= 0 ? normalized[(idx + marker.Length)..] : normalized;
        return $"{slug}.SemanticModel/definition/{relative}";
    }

    private static string SanitizeSlug(string value)
    {
        var chars = value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray();
        var slug = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "dashboard-template" : slug;
    }
}

internal static class DashboardTemplateControllerExtensions
{
    /// <summary>A null/blob-path fallback (rather than a real SAS URL) means Power BI's cloud
    /// service has nothing reachable to fetch — deploy is skipped rather than attempted knowing
    /// it will fail on the refresh step.</summary>
    public static bool HasSasScheme(this string? downloadUrl) =>
        !string.IsNullOrWhiteSpace(downloadUrl) && downloadUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase);
}
