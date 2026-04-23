using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.InsightsEngine;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Services;
using StudioTechBI.Application.Utilities;
using StudioTechBI.Infrastructure.Clients;

namespace StudioTechBI.API.Controllers;

[ApiController]
[Route("api/insights-engine")]
[Authorize]
public sealed class InsightsEngineController : ControllerBase
{
    private readonly InsightsEngineClient _client;
    private readonly DataSamplingService _sampling;
    private readonly IInsightTemplateVerificationService _templateVerification;
    private readonly IBlobStorageService _blobStorage;
    private readonly IClientResolver _clientResolver;
    private readonly IClientByCompanyQuery _clientByCompanyQuery;
    private readonly IClientService _clientService;
    private readonly ILogger<InsightsEngineController> _logger;

    public InsightsEngineController(
        InsightsEngineClient client,
        DataSamplingService sampling,
        IInsightTemplateVerificationService templateVerification,
        IBlobStorageService blobStorage,
        IClientResolver clientResolver,
        IClientByCompanyQuery clientByCompanyQuery,
        IClientService clientService,
        ILogger<InsightsEngineController> logger)
    {
        _client = client;
        _sampling = sampling;
        _templateVerification = templateVerification;
        _blobStorage = blobStorage;
        _clientResolver = clientResolver;
        _clientByCompanyQuery = clientByCompanyQuery;
        _clientService = clientService;
        _logger = logger;
    }

    private async Task<bool> CanAccessClientAsync(Guid clientId, CancellationToken ct)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email)) return false;

        var fromCompanies = await _clientByCompanyQuery.GetClientsForUserEmailAsync(email, ct);
        if (fromCompanies.Any(c => c.ClientId == clientId))
            return true;

        var claimCode = User.FindFirstValue("client_code");
        if (string.IsNullOrEmpty(claimCode)) return false;

        var fromClaim = await _clientService.GetByClientCodeOrIdAsync(claimCode, ct);
        return fromClaim != null && fromClaim.ClientId == clientId;
    }

    /// <summary>
    /// Returns a small tabular sample from the latest CSV/XLSX under <c>{client}/accounting/created/</c> (same source as report/model flows).
    /// </summary>
    [HttpGet("report-data-sample")]
    public async Task<IActionResult> GetReportDataSample(
        [FromQuery] string clientCode,
        [FromQuery] int maxRows = 100,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clientCode))
            return BadRequest(ApiResponse<object>.ErrorResponse("clientCode is required."));

        var mr = maxRows <= 0 ? 100 : Math.Min(maxRows, CsvSampleExtractor.DefaultMaxRows);

        var client = await _clientResolver.ResolveAsync(clientCode.Trim(), ct);
        if (client == null)
            return NotFound(ApiResponse<object>.ErrorResponse("Client not found."));

        if (!await CanAccessClientAsync(client.Id, ct))
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.ErrorResponse("You do not have access to this client."));

        var folder = (client.BlobFolderPath ?? client.ClientCode ?? client.Id.ToString()).Trim();
        var prefix = $"{folder}/accounting/created/";
        var blobPath = await _blobStorage.GetLatestBlobPathByPrefixAsync(prefix, ".xlsx", ct)
            ?? await _blobStorage.GetLatestBlobPathByPrefixAsync(prefix, ".csv", ct);

        if (string.IsNullOrEmpty(blobPath))
        {
            _logger.LogWarning("No .xlsx or .csv under {Prefix} for client {ClientId}.", prefix, client.Id);
            return NotFound(ApiResponse<object>.ErrorResponse(
                $"No data file found under '{prefix}'. Upload a CSV or XLSX to accounting/created."));
        }

        var sample = await _sampling.CreateSampleAsync(blobPath, client.Id, mr, ct);

        var rows = sample.SampleRows
            .Select(r => r.ToDictionary(kv => kv.Key, kv => (object?)kv.Value, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var payload = new ReportDataSampleResponse
        {
            Columns = sample.Columns,
            Rows = rows,
            RowCount = rows.Count,
            Truncated = rows.Count >= mr
        };

        return Ok(ApiResponse<ReportDataSampleResponse>.SuccessResponse(payload, "Sample loaded."));
    }

    public sealed class SuggestApiRequest
    {
        public string ClientId { get; set; } = "";
        public int MaxRows { get; set; } = 100;
        public string? UserPrompt { get; set; }
        public JsonElement Sample { get; set; }
    }

    [HttpPost("transformations/suggest")]
    public async Task<IActionResult> SuggestTransformations([FromBody] SuggestApiRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.ClientId))
            return BadRequest(ApiResponse<object>.ErrorResponse("ClientId is required."));

        if (body.MaxRows <= 0 || body.MaxRows > 100)
            body.MaxRows = 100;

        var req = new TransformSuggestRequest
        {
            ClientId = body.ClientId.Trim(),
            MaxRows = body.MaxRows,
            UserPrompt = string.IsNullOrWhiteSpace(body.UserPrompt) ? null : body.UserPrompt.Trim(),
            Sample = body.Sample.ValueKind == JsonValueKind.Undefined ? JsonSerializer.SerializeToElement(new { }) : body.Sample
        };

        var resp = await _client.SuggestTransformationsAsync(req, ct);
        var enriched = await _templateVerification.EnrichWithVerifiedTemplatesAsync(
            resp,
            req.UserPrompt,
            ct);
        return Ok(ApiResponse<InsightsWithTemplatesResponse>.SuccessResponse(enriched, "Suggestions generated."));
    }

    public sealed class SuggestFromBlobApiRequest
    {
        public string ClientId { get; set; } = "";
        public string BlobPath { get; set; } = "";
        public int MaxRows { get; set; } = 100;
        public string? UserPrompt { get; set; }
    }

    /// <summary>
    /// Samples the first N rows from a blob (CSV/XLSX) and requests a transformation plan from InsightsEngine.
    /// </summary>
    [HttpPost("transformations/suggest-from-blob")]
    public async Task<IActionResult> SuggestTransformationsFromBlob([FromBody] SuggestFromBlobApiRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.ClientId))
            return BadRequest(ApiResponse<object>.ErrorResponse("ClientId is required."));
        if (string.IsNullOrWhiteSpace(body.BlobPath))
            return BadRequest(ApiResponse<object>.ErrorResponse("BlobPath is required."));

        var maxRows = body.MaxRows <= 0 ? 100 : Math.Min(body.MaxRows, 100);

        if (!Guid.TryParse(body.ClientId.Trim(), out var clientId))
            return BadRequest(ApiResponse<object>.ErrorResponse("ClientId must be a GUID for blob sampling."));

        var sample = await _sampling.CreateSampleAsync(body.BlobPath, clientId, maxRows, ct);
        var element = JsonSerializer.SerializeToElement(sample.SampleRows);

        var req = new TransformSuggestRequest
        {
            ClientId = body.ClientId.Trim(),
            MaxRows = maxRows,
            UserPrompt = string.IsNullOrWhiteSpace(body.UserPrompt) ? null : body.UserPrompt.Trim(),
            Sample = element
        };

        var resp = await _client.SuggestTransformationsAsync(req, ct);
        var enriched = await _templateVerification.EnrichWithVerifiedTemplatesAsync(
            resp,
            req.UserPrompt,
            ct);
        return Ok(ApiResponse<InsightsWithTemplatesResponse>.SuccessResponse(enriched, "Suggestions generated."));
    }
}

