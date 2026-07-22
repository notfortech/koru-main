using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.BindDeploy;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.DashboardTemplate;
using StudioTechBI.Application.DTOs.ReportDesigner;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Application.Utilities;

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
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private static readonly string[] AllowedExtensions = { ".xlsx", ".xls" };
    private static readonly TimeSpan SasValidFor = TimeSpan.FromHours(1);

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
    private readonly DashboardTemplateOptions _options;
    private readonly ILogger<DashboardTemplateController> _logger;

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
        IOptions<DashboardTemplateOptions> options,
        ILogger<DashboardTemplateController> logger)
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
        _options = options.Value;
        _logger = logger;
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

    /// <summary>
    /// POST /api/dashboard-template/generate
    /// Multipart form: file (the client's Excel upload), clientId, blueprint (raw JSON string —
    /// either the Analytics Blueprint from POST /api/report-designer/generate-model, or a Design
    /// Blueprint — auto-detected, see BlueprintFormatDetector).
    /// </summary>
    [HttpPost("generate")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<IActionResult> GenerateAsync(
        IFormFile file,
        [FromForm] string clientId,
        [FromForm] string blueprint,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.ErrorResponse("No file uploaded."));

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(ApiResponse<object>.ErrorResponse("File exceeds the 50 MB limit."));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(ApiResponse<object>.ErrorResponse(
                $"Unsupported file type '{ext}'. Allowed: {string.Join(", ", AllowedExtensions)}"));

        if (string.IsNullOrWhiteSpace(clientId))
            return BadRequest(ApiResponse<object>.ErrorResponse("clientId is required."));

        JsonDocument blueprintDoc;
        try
        {
            blueprintDoc = JsonDocument.Parse(blueprint);
        }
        catch (JsonException)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("blueprint is not valid JSON."));
        }

        using var _ = blueprintDoc;

        var client = await _clientResolver.ResolveAsync(clientId, cancellationToken);
        if (client is null)
            return NotFound(ApiResponse<object>.ErrorResponse($"Client '{clientId}' not found."));

        var correlationId = Guid.NewGuid().ToString();
        var blueprintElement = blueprintDoc.RootElement;
        var isDesignBlueprint = BlueprintFormatDetector.IsDesignBlueprint(blueprintElement);
        var designTemplateId = isDesignBlueprint ? ReadStringProperty(blueprintElement, "templateId") : null;
        var designTier = isDesignBlueprint ? ReadStringProperty(blueprintElement, "tier") : null;
        var designLabel = isDesignBlueprint ? ReadStringProperty(blueprintElement, "label") : null;

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

        // Timestamped so repeat generations for the same client get genuinely new Power BI
        // artifacts (new datasetId/reportId) instead of colliding with — and Overwrite-ing — a
        // prior generation's dataset. Necessary because, for now, every client's generations
        // share the one configured workspace (see DashboardTemplateOptions) rather than each
        // having their own — naming is the only differentiator until that changes.
        var generatedAt = DateTime.UtcNow;
        var reportDisplayName = $"{client.ClientName} — {designLabel ?? "Dashboard Template"} — {generatedAt:yyyy-MM-dd HH:mm:ss} UTC";
        var generation = _reportVisualGenerator.Generate(blueprintElement, blended.Tables, patchedFiles, reportDisplayName);

        var deployed = false;
        string? workspaceId = null;
        string? datasetId = null;
        string? reportId = null;
        var visualLog = new List<string>(generation.Log);

        if (!downloadUrl.HasSasScheme())
        {
            visualLog.Add("Deploy skipped: no SAS download URL was available for the blended dataset — Power BI would not be able to fetch it.");
        }
        else
        {
            try
            {
                var slug = SanitizeSlug($"{client.ClientName}-{correlationId[..8]}");
                var reportFolder = $"{slug}.Report";
                var semanticModelFolder = $"{slug}.SemanticModel";

                var pbipFiles = new List<PbipFileDto>();
                pbipFiles.AddRange(patchedFiles.Select(f => new PbipFileDto(ToSemanticModelPath(slug, f.Path), f.Content)));
                pbipFiles.Add(new PbipFileDto($"{reportFolder}/report.json", generation.ReportJson));
                pbipFiles.Add(new PbipFileDto($"{reportFolder}/.platform", generation.PlatformManifestJson));
                pbipFiles.Add(new PbipFileDto($"{reportFolder}/definition.pbir", PbipSkeletonBuilder.BuildReportDefinitionPbir(semanticModelFolder)));
                pbipFiles.Add(new PbipFileDto($"{semanticModelFolder}/.platform", PbipSkeletonBuilder.BuildSemanticModelPlatformManifest(reportDisplayName)));
                pbipFiles.Add(new PbipFileDto($"{semanticModelFolder}/definition.pbism", PbipSkeletonBuilder.BuildSemanticModelDefinitionPbism()));
                pbipFiles.Add(new PbipFileDto($"{slug}.pbip", PbipSkeletonBuilder.BuildTopLevelProjectJson(reportFolder)));

                var importRequest = new PbipImportRequest(
                    ClientName: client.ClientName,
                    ReportName: reportDisplayName,
                    WorkspaceName: _options.WorkspaceName,
                    Files: pbipFiles);

                var importResult = await _pbipImportClient.ImportAsync(importRequest, correlationId, cancellationToken);
                visualLog.AddRange(importResult.Steps);

                await _powerBiAssetWriter.WriteAsync(
                    clientId: client.Id,
                    templateId: null,
                    workspaceId: importResult.WorkspaceId,
                    datasetId: importResult.DatasetId,
                    reportId: importResult.ReportId,
                    reportType: "dashboard-template-generated",
                    cancellationToken: cancellationToken);

                deployed = true;
                workspaceId = importResult.WorkspaceId;
                datasetId = importResult.DatasetId;
                reportId = importResult.ReportId;

                _logger.LogInformation(
                    "DashboardTemplate.Deployed ClientId={ClientId} CorrelationId={CorrelationId} WorkspaceId={WorkspaceId} DatasetId={DatasetId} ReportId={ReportId}",
                    client.Id, correlationId, workspaceId, datasetId, reportId);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "DashboardTemplate.DeployFailed ClientId={ClientId} CorrelationId={CorrelationId}", client.Id, correlationId);
                visualLog.Add($"Deploy to Power BI failed: {ex.Message}. The blended dataset and semantic model below are still available to download and fix manually.");
            }
        }

        var summary = $"{uploadedCount} of {blended.Provenance.Count} columns used from your file; " +
                      $"{mockedCount} column(s) mocked — see the provenance log for which ones." +
                      (tmdlPatched ? "" : " Note: the authored semantic model's data source could not be auto-patched; wire SourceFilePath manually.") +
                      (deployed ? " A live Power BI report was generated with visuals." : " The Power BI report was not deployed — see the log for why.");

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
                designLabel),
            "Dashboard template generated."));
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
