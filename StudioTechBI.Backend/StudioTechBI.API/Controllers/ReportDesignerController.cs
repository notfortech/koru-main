using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.Connectors;
using StudioTechBI.Application.DTOs.ReportDesigner;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Utilities;
using StudioTechBI.Infrastructure.Services;

namespace StudioTechBI.API.Controllers;

/// <summary>
/// Report Designer pipeline: schema extraction → AI model generation.
/// Data sources are read-only; only structural metadata (no data values) is sent to AI.
/// </summary>
[ApiController]
[Route("api/report-designer")]
[Authorize]
public class ReportDesignerController : ControllerBase
{
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private static readonly string[] AllowedExtensions = { ".xlsx", ".xls", ".csv" };

    private readonly IReportDesignerClient _reportDesignerClient;
    private readonly SqlSchemaReaderService _sqlSchemaReader;
    private readonly SharePointSchemaService _sharePointSchemaService;
    private readonly IReportDesignerConsentService _consentService;
    private readonly IReportMatchService _reportMatchService;
    private readonly IReportDataUsageConsentService _dataUsageConsentService;
    private readonly IClientResolver _clientResolver;
    private readonly ILogger<ReportDesignerController> _logger;

    public ReportDesignerController(
        IReportDesignerClient reportDesignerClient,
        SqlSchemaReaderService sqlSchemaReader,
        SharePointSchemaService sharePointSchemaService,
        IReportDesignerConsentService consentService,
        IReportMatchService reportMatchService,
        IReportDataUsageConsentService dataUsageConsentService,
        IClientResolver clientResolver,
        ILogger<ReportDesignerController> logger)
    {
        _reportDesignerClient = reportDesignerClient;
        _sqlSchemaReader = sqlSchemaReader;
        _sharePointSchemaService = sharePointSchemaService;
        _consentService = consentService;
        _reportMatchService = reportMatchService;
        _dataUsageConsentService = dataUsageConsentService;
        _clientResolver = clientResolver;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/report-designer/extract-schema/excel
    /// Accepts an Excel or CSV file upload and returns structural schema metadata only.
    /// File content is streamed and discarded — never stored.
    /// </summary>
    [HttpPost("extract-schema/excel")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<IActionResult> ExtractExcelSchemaAsync(
        IFormFile file,
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

        try
        {
            TableSchemaDto table;

            await using var stream = file.OpenReadStream();

            if (ext == ".csv")
            {
                var (cols, _) = await CsvSampleExtractor.ExtractAsync(stream, 1, cancellationToken);
                table = new TableSchemaDto(
                    TableName: Path.GetFileNameWithoutExtension(file.FileName),
                    SheetName: Path.GetFileNameWithoutExtension(file.FileName),
                    RowCount: 0,
                    Columns: cols.Select(c => new ColumnSchemaDto(c, "string", true, null)).ToList());
            }
            else
            {
                table = await ExcelSchemaExtractor.ExtractAsync(stream, file.FileName, cancellationToken);
            }

            var allCols = table.Columns.Select(c => c.ColumnName).ToList();
            var schemaHash = SchemaHashHelper.ComputeSchemaHash(allCols);

            var schema = new ExtractedSchemaDto(
                Source: "excel",
                FileName: file.FileName,
                Tables: new List<TableSchemaDto> { table },
                SchemaHash: schemaHash,
                ExtractedAt: DateTimeOffset.UtcNow);

            return Ok(ApiResponse<ExtractedSchemaDto>.SuccessResponse(schema, "Schema extracted successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "An error occurred while extracting schema from the uploaded file."));
        }
    }

    /// <summary>
    /// POST /api/report-designer/extract-schema/sql
    /// Connects to SQL Server (read-only), reads schema metadata via GetSchema(), disconnects.
    /// No data rows are fetched. Password is never logged.
    /// </summary>
    [HttpPost("extract-schema/sql")]
    public async Task<IActionResult> ExtractSqlSchemaAsync(
        [FromBody] SqlConnectionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Host) ||
            string.IsNullOrWhiteSpace(request.Database) ||
            string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "Host, Database, and Username are required."));

        try
        {
            var schema = await _sqlSchemaReader.ReadSchemaAsync(request, cancellationToken);
            return Ok(ApiResponse<ExtractedSchemaDto>.SuccessResponse(schema, "SQL schema extracted successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "Failed to connect to the SQL Server database. Check host, credentials, and network access."));
        }
    }

    /// <summary>
    /// POST /api/report-designer/sharepoint/browse
    /// Lists folders and supported data files at the root of a SharePoint site's document library.
    /// </summary>
    [HttpPost("sharepoint/browse")]
    public async Task<IActionResult> BrowseSharePointAsync(
        [FromBody] SharePointBrowseRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SiteUrl))
            return BadRequest(ApiResponse<object>.ErrorResponse("SiteUrl is required."));

        try
        {
            var files = await _sharePointSchemaService.BrowseAsync(request.SiteUrl, cancellationToken);
            return Ok(ApiResponse<List<FileItem>>.SuccessResponse(files, $"Found {files.Count} item(s)."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "Failed to browse the SharePoint site. Verify the URL and site permissions."));
        }
    }

    /// <summary>
    /// POST /api/report-designer/extract-schema/sharepoint
    /// Downloads a SharePoint file, extracts schema metadata, and discards the stream immediately.
    /// No data values are returned or stored.
    /// </summary>
    [HttpPost("extract-schema/sharepoint")]
    public async Task<IActionResult> ExtractSharePointSchemaAsync(
        [FromBody] SharePointExtractRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SiteUrl) ||
            string.IsNullOrWhiteSpace(request.DriveItemId) ||
            string.IsNullOrWhiteSpace(request.FileName))
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "SiteUrl, DriveItemId, and FileName are required."));

        try
        {
            var schema = await _sharePointSchemaService.ExtractSchemaAsync(
                request.SiteUrl, request.DriveItemId, request.FileName, cancellationToken);
            return Ok(ApiResponse<ExtractedSchemaDto>.SuccessResponse(schema, "SharePoint schema extracted successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "Failed to extract schema from the SharePoint file."));
        }
    }

    /// <summary>
    /// POST /api/report-designer/consent
    /// Records (or declines) a client's consent to send a specific schema shape's
    /// metadata to the Report Designer AI. Must be granted before /generate-model
    /// will accept that (client, schema) pair. Declining is logged, not persisted —
    /// callers should fall back to manual template selection.
    /// </summary>
    [HttpPost("consent")]
    public async Task<IActionResult> RecordConsentAsync(
        [FromBody] ReportDesignerConsentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
            return BadRequest(ApiResponse<object>.ErrorResponse("ClientId is required."));
        if (string.IsNullOrWhiteSpace(request.SchemaHash))
            return BadRequest(ApiResponse<object>.ErrorResponse("SchemaHash is required."));

        var client = await _clientResolver.ResolveAsync(request.ClientId, cancellationToken);
        if (client is null)
            return NotFound(ApiResponse<object>.ErrorResponse("Client not found."));

        var decidedAt = DateTimeOffset.UtcNow;

        if (!request.ConsentGranted)
        {
            var email = User.FindFirstValue(ClaimTypes.Email) ?? "unknown";
            _logger.LogInformation(
                "ReportDesigner.ConsentDeclined ClientId={ClientId} SchemaHash={SchemaHash} DecidedBy={DecidedBy}",
                client.Id, request.SchemaHash, email);

            return Ok(ApiResponse<ReportDesignerConsentResponse>.SuccessResponse(
                new ReportDesignerConsentResponse(Granted: false, request.SchemaHash, decidedAt),
                "Consent declined. AI matching will be skipped for this schema — choose a template manually."));
        }

        var approvedBy = User.FindFirstValue(ClaimTypes.Email) ?? "unknown";
        await _consentService.GrantConsentAsync(client.Id, request.SchemaHash, approvedBy, cancellationToken);

        return Ok(ApiResponse<ReportDesignerConsentResponse>.SuccessResponse(
            new ReportDesignerConsentResponse(Granted: true, request.SchemaHash, decidedAt),
            "Consent recorded."));
    }

    /// <summary>
    /// POST /api/report-designer/generate-model
    /// Sends extracted schema metadata (no data values) to the Report Designer AI endpoint.
    /// Returns a star schema recommendation and template scores.
    /// Requires that POST /consent was already called with ConsentGranted = true for the
    /// same (ClientId, Schema.SchemaHash) pair — the AI is never called without it.
    /// </summary>
    [HttpPost("generate-model")]
    public async Task<IActionResult> GenerateReportModelAsync(
        [FromBody] GenerateReportModelRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
            return BadRequest(ApiResponse<object>.ErrorResponse("ClientId is required."));

        if (request.Schema is null || request.Schema.Tables.Count == 0)
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "Schema is required and must contain at least one table."));

        var client = await _clientResolver.ResolveAsync(request.ClientId, cancellationToken);
        if (client is null)
            return NotFound(ApiResponse<object>.ErrorResponse("Client not found."));

        var hasConsent = await _consentService.HasConsentAsync(client.Id, request.Schema.SchemaHash, cancellationToken);
        if (!hasConsent)
        {
            _logger.LogWarning(
                "ReportDesigner.GenerateBlockedNoConsent ClientId={ClientId} SchemaHash={SchemaHash}",
                client.Id, request.Schema.SchemaHash);

            return StatusCode(403, ApiResponse<object>.ErrorResponse(
                "AI consent is required before generating a report model. " +
                "Call POST /api/report-designer/consent with ConsentGranted = true for this schema first."));
        }

        var correlationId = Guid.NewGuid().ToString();

        try
        {
            var result = await _reportDesignerClient.GenerateReportModelAsync(
                request, correlationId, cancellationToken);

            return Ok(ApiResponse<GenerateReportModelResponse>.SuccessResponse(
                result, "Report model generated successfully."));
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(
                (int)(ex.StatusCode ?? System.Net.HttpStatusCode.BadGateway),
                ApiResponse<object>.ErrorResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "An error occurred while generating the report model."));
        }
    }

    /// <summary>
    /// POST /api/report-designer/match
    /// Scores the extracted schema against the Approved SchemaModel directory. Deterministic
    /// name-overlap matching runs first; if its confidence is below the escalation gate, this
    /// calls stbi_transformers for a semantic match against the same directory, or a brand-new
    /// SchemaModel proposal (persisted as ReviewStatus = PendingReview) when nothing fits well.
    /// Result is persisted as a ReportMatchDraft either way. Requires the same prior consent
    /// grant as /generate-model.
    /// </summary>
    [HttpPost("match")]
    public async Task<IActionResult> MatchAsync(
        [FromBody] ReportMatchRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
            return BadRequest(ApiResponse<object>.ErrorResponse("ClientId is required."));

        if (request.Schema is null || request.Schema.Tables.Count == 0)
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "Schema is required and must contain at least one table."));

        var client = await _clientResolver.ResolveAsync(request.ClientId, cancellationToken);
        if (client is null)
            return NotFound(ApiResponse<object>.ErrorResponse("Client not found."));

        var hasConsent = await _consentService.HasConsentAsync(client.Id, request.Schema.SchemaHash, cancellationToken);
        if (!hasConsent)
        {
            _logger.LogWarning(
                "ReportDesigner.MatchBlockedNoConsent ClientId={ClientId} SchemaHash={SchemaHash}",
                client.Id, request.Schema.SchemaHash);

            return StatusCode(403, ApiResponse<object>.ErrorResponse(
                "AI consent is required before matching against the model directory. " +
                "Call POST /api/report-designer/consent with ConsentGranted = true for this schema first."));
        }

        try
        {
            var result = await _reportMatchService.MatchAsync(client.Id, request.Schema, cancellationToken);

            var message = result switch
            {
                { SchemaModelId: null } => "No confident match found in the model directory.",
                { PendingSupportReview: true } => $"No good fit in the directory — AI proposed a new model '{result.SchemaModelName}', pending support review before it can be used.",
                { MatchSource: "AiProposedNew" } => $"No good fit in the directory — AI proposed a new model '{result.SchemaModelName}' and a matching dashboard template.",
                { MatchSource: "AiMatched" } => $"AI matched '{result.SchemaModelName}' (confidence {result.Confidence:P0}).",
                _ => $"Matched '{result.SchemaModelName}' (confidence {result.Confidence:P0}).",
            };

            return Ok(ApiResponse<ReportMatchResultDto>.SuccessResponse(result, message));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "An error occurred while matching the schema against the model directory."));
        }
    }

    /// <summary>
    /// POST /api/report-designer/match/{draftId}/data-usage-consent
    /// Consent #2: confirms the report will use this specific (matched) data, distinct from
    /// Consent #1's "send my schema to AI" grant. Append-only — recorded every time this is
    /// called, no upsert. The flow stops here for now; Save/Publish land in a later story.
    /// </summary>
    [HttpPost("match/{draftId:guid}/data-usage-consent")]
    public async Task<IActionResult> RecordDataUsageConsentAsync(
        Guid draftId,
        [FromBody] DataUsageConsentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
            return BadRequest(ApiResponse<object>.ErrorResponse("ClientId is required."));

        var client = await _clientResolver.ResolveAsync(request.ClientId, cancellationToken);
        if (client is null)
            return NotFound(ApiResponse<object>.ErrorResponse("Client not found."));

        var approvedBy = User.FindFirstValue(ClaimTypes.Email) ?? "unknown";

        try
        {
            var approvedAt = await _dataUsageConsentService.RecordConsentAsync(draftId, client.Id, approvedBy, cancellationToken);
            return Ok(ApiResponse<DataUsageConsentResponse>.SuccessResponse(
                new DataUsageConsentResponse(draftId, approvedAt), "Data usage consent recorded."));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<object>.ErrorResponse(ex.Message));
        }
    }
}
