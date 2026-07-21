using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.DashboardTemplate;
using StudioTechBI.Application.DTOs.ReportDesigner;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Utilities;

namespace StudioTechBI.API.Controllers;

/// <summary>
/// Dashboard Template Generator (Phase 1+2) — a wholly new, separate flow from Report
/// Designer's publish (ReportDesignerController.PublishAsync). Given a client's uploaded file
/// and an already-generated blueprint (from POST /api/report-designer/generate-model), blends
/// real values with clearly-labeled mock values for any columns the upload doesn't cover,
/// patches the authored TMDL's data source to a short-lived SAS URL, and returns everything
/// (provenance log, blended dataset, patched TMDL) for the client to inspect or take further.
/// Does not deploy to Power BI — that remains Report Designer's publish flow, and report/visual
/// generation is a later phase, not built yet.
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
    private readonly IClientResolver _clientResolver;
    private readonly ILogger<DashboardTemplateController> _logger;

    public DashboardTemplateController(
        IReportDesignerClient reportDesignerClient,
        IDataBlendService blendService,
        IWorkbookWriter workbookWriter,
        IBlobStorageService blobStorage,
        IBlobSasUriProvider sasUriProvider,
        IClientResolver clientResolver,
        ILogger<DashboardTemplateController> logger)
    {
        _reportDesignerClient = reportDesignerClient;
        _blendService = blendService;
        _workbookWriter = workbookWriter;
        _blobStorage = blobStorage;
        _sasUriProvider = sasUriProvider;
        _clientResolver = clientResolver;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/dashboard-template/generate
    /// Multipart form: file (the client's Excel upload), clientId, blueprint (raw JSON string —
    /// the same blueprint object returned earlier by POST /api/report-designer/generate-model).
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

        AuthorTmdlResponse authored;
        try
        {
            authored = await _reportDesignerClient.AuthorTmdlAsync(blueprintElement, correlationId, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "DashboardTemplate.AuthorTmdlFailed ClientId={ClientId} CorrelationId={CorrelationId}", client.Id, correlationId);
            return StatusCode(502, ApiResponse<object>.ErrorResponse("Failed to author the semantic model. Please try again."));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "DashboardTemplate.AuthorTmdlFailed ClientId={ClientId} CorrelationId={CorrelationId}", client.Id, correlationId);
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
        var summary = $"{uploadedCount} of {blended.Provenance.Count} columns used from your file; " +
                      $"{mockedCount} column(s) mocked — see the provenance log for which ones." +
                      (tmdlPatched ? "" : " Note: the authored semantic model's data source could not be auto-patched; wire SourceFilePath manually.");

        _logger.LogInformation(
            "DashboardTemplate.Generated ClientId={ClientId} CorrelationId={CorrelationId} Uploaded={Uploaded} Mocked={Mocked} TmdlPatched={TmdlPatched}",
            client.Id, correlationId, uploadedCount, mockedCount, tmdlPatched);

        return Ok(ApiResponse<GenerateDashboardTemplateResponse>.SuccessResponse(
            new GenerateDashboardTemplateResponse(
                correlationId,
                blended.Provenance,
                blobPath,
                downloadUrl,
                patchedFiles,
                tmdlPatched,
                summary),
            "Dashboard template generated."));
    }
}
