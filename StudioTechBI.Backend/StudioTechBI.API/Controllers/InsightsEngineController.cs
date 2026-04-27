using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.InsightsEngine;
using StudioTechBI.Application.DTOs.InsightsEngine.Canonical;
using StudioTechBI.Application.DTOs.Templates;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Application.Services;
using StudioTechBI.Application.Utilities;
using StudioTechBI.Domain.Entities;
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
    private readonly ITemplateMatchingService _templateMatching;
    private readonly IOptionsMonitor<InsightsEngineOptions> _insightsEngineOptions;
    private readonly ILogger<InsightsEngineController> _logger;

    public InsightsEngineController(
        InsightsEngineClient client,
        DataSamplingService sampling,
        IInsightTemplateVerificationService templateVerification,
        IBlobStorageService blobStorage,
        IClientResolver clientResolver,
        IClientByCompanyQuery clientByCompanyQuery,
        IClientService clientService,
        ITemplateMatchingService templateMatching,
        IOptionsMonitor<InsightsEngineOptions> insightsEngineOptions,
        ILogger<InsightsEngineController> logger)
    {
        _client = client;
        _sampling = sampling;
        _templateVerification = templateVerification;
        _blobStorage = blobStorage;
        _clientResolver = clientResolver;
        _clientByCompanyQuery = clientByCompanyQuery;
        _clientService = clientService;
        _templateMatching = templateMatching;
        _insightsEngineOptions = insightsEngineOptions;
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
    /// Resolves the target client: either from JWT <c>client_code</c> (when <paramref name="useSelectedClient"/>) or from <paramref name="clientCodeOrId"/>.
    /// </summary>
    private async Task<(Client? client, IActionResult? error)> ResolveTargetClientAsync(
        string? clientCodeOrId,
        bool useSelectedClient,
        CancellationToken ct)
    {
        string? key;
        if (useSelectedClient)
        {
            var claim = User.FindFirstValue("client_code");
            if (string.IsNullOrEmpty(claim))
            {
                return (null, BadRequest(ApiResponse<object>.ErrorResponse(
                    "useSelectedClient is true but the access token has no client_code claim.")));
            }
            key = claim.Trim();
        }
        else
        {
            if (string.IsNullOrWhiteSpace(clientCodeOrId))
            {
                return (null, BadRequest(ApiResponse<object>.ErrorResponse(
                    "clientCode is required when useSelectedClient is false.")));
            }
            key = clientCodeOrId.Trim();
        }

        var client = await _clientResolver.ResolveAsync(key, ct);
        if (client == null)
            return (null, NotFound(ApiResponse<object>.ErrorResponse("Client not found.")));

        if (!await CanAccessClientAsync(client.Id, ct))
            return (null, StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.ErrorResponse("You do not have access to this client.")));

        return (client, null);
    }

    public sealed class ResolveBlobResponse
    {
        public Guid ClientId { get; set; }
        public string ClientCode { get; set; } = "";
        public string BlobPath { get; set; } = "";
    }

    /// <summary>Primary path for the SPA: JSON body, no GET fallback required.</summary>
    [HttpPost("resolve-blob")]
    public Task<IActionResult> PostResolveBlob([FromBody] ResolveBlobRequest? body, CancellationToken ct) =>
        ExecuteResolveBlobCore(body?.ClientCode, body?.UseSelectedClient == true, ct);

    /// <summary>
    /// Resolves the latest uploaded report blob under <c>{client}/accounting/created/</c> for a given client code/id.
    /// </summary>
    [HttpGet("resolve-blob")]
    public Task<IActionResult> GetResolveBlob([FromQuery] string clientCode, CancellationToken ct) =>
        ExecuteResolveBlobCore(clientCode, useSelectedClient: false, ct);

    private async Task<IActionResult> ExecuteResolveBlobCore(
        string? clientCode,
        bool useSelectedClient,
        CancellationToken ct)
    {
        var (client, err) = await ResolveTargetClientAsync(clientCode, useSelectedClient, ct);
        if (err != null) return err;
        if (client == null) return NotFound();

        var folder = (client.BlobFolderPath ?? client.ClientCode ?? client.Id.ToString()).Trim();
        var prefix = $"{folder}/accounting/created/";
        var blobPath = await _blobStorage.GetLatestBlobPathByPrefixAsync(prefix, ".xlsx", ct)
            ?? await _blobStorage.GetLatestBlobPathByPrefixAsync(prefix, ".csv", ct);

        if (string.IsNullOrWhiteSpace(blobPath))
            return NotFound(ApiResponse<object>.ErrorResponse($"No .xlsx or .csv found under '{prefix}'."));

        return Ok(ApiResponse<ResolveBlobResponse>.SuccessResponse(new ResolveBlobResponse
        {
            ClientId = client.Id,
            ClientCode = (client.ClientCode ?? clientCode)?.Trim() ?? string.Empty,
            BlobPath = blobPath
        }, "Blob resolved."));
    }

    /// <summary>Primary path for the SPA: JSON body.</summary>
    [HttpPost("report-data-sample")]
    public Task<IActionResult> PostReportDataSample([FromBody] ReportDataSampleRequest? body, CancellationToken ct) =>
        ExecuteReportDataSampleCore(body?.ClientCode, body?.MaxRows ?? 100, body?.UseSelectedClient == true, ct);

    /// <summary>
    /// Matches the client's report data schema to known dashboard templates using metadata (no PBIX parsing).
    /// AI (if enabled) refines column mapping after deterministic selection.
    /// </summary>
    [HttpPost("templates/match-from-blob")]
    public async Task<IActionResult> MatchTemplatesFromBlob([FromBody] TemplateMatchRequest? body, CancellationToken ct)
    {
        var useSelected = body?.UseSelectedClient == true;
        var key = useSelected ? User.FindFirstValue("client_code") : body?.ClientCode;
        if (useSelected && string.IsNullOrWhiteSpace(key))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "useSelectedClient is true but the access token has no client_code claim."));
        }

        var resp = await _templateMatching.MatchFromBlobAsync(
            clientCodeOrId: key,
            useSelectedClient: useSelected,
            blobPath: body?.BlobPath,
            maxRows: body?.MaxRows ?? 100,
            useAiRefinement: body?.UseAiRefinement != false,
            cancellationToken: ct);

        return Ok(ApiResponse<TemplateMatchResponse>.SuccessResponse(resp, "Templates matched."));
    }

    /// <summary>Same as <see cref="MatchTemplatesFromBlob"/> but uses UI-provided column names (no blob I/O).</summary>
    [HttpPost("templates/match-from-sample")]
    public async Task<IActionResult> MatchTemplatesFromSample([FromBody] TemplateMatchFromSampleRequest? body, CancellationToken ct)
    {
        var useSelected = body?.UseSelectedClient == true;
        var key = useSelected ? User.FindFirstValue("client_code") : body?.ClientCode;
        if (useSelected && string.IsNullOrWhiteSpace(key))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "useSelectedClient is true but the access token has no client_code claim."));
        }

        if (body?.Columns is null || body.Columns.Count == 0)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("columns is required (at least one column name)."));
        }

        var resp = await _templateMatching.MatchFromColumnsAsync(
            clientCodeOrId: key,
            useSelectedClient: useSelected,
            columns: body.Columns,
            useAiRefinement: body.UseAiRefinement,
            cancellationToken: ct);

        return Ok(ApiResponse<TemplateMatchResponse>.SuccessResponse(resp, "Templates matched."));
    }

    /// <summary>
    /// Returns a small tabular sample from the latest CSV/XLSX under <c>{client}/accounting/created/</c>.
    /// </summary>
    [HttpGet("report-data-sample")]
    public Task<IActionResult> GetReportDataSample(
        [FromQuery] string clientCode,
        [FromQuery] int maxRows = 100,
        CancellationToken ct = default) =>
        ExecuteReportDataSampleCore(clientCode, maxRows, useSelectedClient: false, ct);

    private async Task<IActionResult> ExecuteReportDataSampleCore(
        string? clientCode,
        int maxRows,
        bool useSelectedClient,
        CancellationToken ct)
    {
        var (client, err) = await ResolveTargetClientAsync(clientCode, useSelectedClient, ct);
        if (err != null) return err;
        if (client == null) return NotFound();

        var mr = maxRows <= 0 ? 100 : Math.Min(maxRows, CsvSampleExtractor.DefaultMaxRows);

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

        var columnNames = InsightsSampleColumnExtractor.ExtractColumnNames(req.Sample);
        var best = await _templateMatching.GetBestCatalogMatchScoreAsync(columnNames, ct);
        var opt = _insightsEngineOptions.CurrentValue;
        if (!CopilotExternalAiGate.ShouldCallExternalInsightsEngine(opt, best))
        {
            var local = BuildCatalogOnlyTransformInsights(columnNames, opt);
            var enriched = await _templateVerification.EnrichWithVerifiedTemplatesAsync(
                local,
                req.UserPrompt,
                ct);
            return Ok(ApiResponse<InsightsWithTemplatesResponse>.SuccessResponse(enriched, "Catalog templates only (external AI not used)."));
        }

        var resp = await _client.SuggestTransformationsAsync(req, ct);
        var enriched2 = await _templateVerification.EnrichWithVerifiedTemplatesAsync(
            resp,
            req.UserPrompt,
            ct);
        return Ok(ApiResponse<InsightsWithTemplatesResponse>.SuccessResponse(enriched2, "Suggestions generated."));
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

        var columnNames = sample.Columns?.Count > 0
            ? (IReadOnlyList<string>)sample.Columns
            : InsightsSampleColumnExtractor.ExtractColumnNames(element);
        var best = await _templateMatching.GetBestCatalogMatchScoreAsync(columnNames, ct);
        var opt = _insightsEngineOptions.CurrentValue;
        if (!CopilotExternalAiGate.ShouldCallExternalInsightsEngine(opt, best))
        {
            var local = BuildCatalogOnlyTransformInsights(columnNames, opt);
            var enriched = await _templateVerification.EnrichWithVerifiedTemplatesAsync(
                local,
                req.UserPrompt,
                ct);
            return Ok(ApiResponse<InsightsWithTemplatesResponse>.SuccessResponse(enriched, "Catalog templates only (external AI not used)."));
        }

        var resp = await _client.SuggestTransformationsAsync(req, ct);
        var enriched2 = await _templateVerification.EnrichWithVerifiedTemplatesAsync(
            resp,
            req.UserPrompt,
            ct);
        return Ok(ApiResponse<InsightsWithTemplatesResponse>.SuccessResponse(enriched2, "Suggestions generated."));
    }

    /// <summary>Same as <see cref="SuggestModelsFromBlob"/> but with a tabular <c>Sample</c> from the client (no blob I/O).</summary>
    [HttpPost("models/suggest")]
    public async Task<IActionResult> SuggestModelsFromSample([FromBody] SuggestApiRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.ClientId))
            return BadRequest(ApiResponse<object>.ErrorResponse("ClientId is required."));

        if (body.MaxRows <= 0 || body.MaxRows > 100)
            body.MaxRows = 100;

        var sampleElement = body.Sample.ValueKind == JsonValueKind.Undefined
            ? JsonSerializer.SerializeToElement(Array.Empty<object>())
            : body.Sample;

        var columnNames = InsightsSampleColumnExtractor.ExtractColumnNames(sampleElement);
        var best = await _templateMatching.GetBestCatalogMatchScoreAsync(columnNames, ct);
        var opt = _insightsEngineOptions.CurrentValue;

        var req = new ModelSuggestRequest
        {
            ClientId = body.ClientId.Trim(),
            MaxRows = body.MaxRows,
            UserPrompt = string.IsNullOrWhiteSpace(body.UserPrompt) ? null : body.UserPrompt.Trim(),
            Sample = sampleElement
        };

        if (!CopilotExternalAiGate.ShouldCallExternalInsightsEngine(opt, best))
        {
            var local = BuildCatalogOnlyModelResponse(columnNames, opt);
            var projected = new TransformSuggestResponse
            {
                Provider = local.Provider,
                Summary = local.AnalysisSummary,
                Columns = local.DetectedSchema
                    .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => new ColumnProfile { Name = c.Name, InferredType = c.InferredType })
                    .ToList()
            };
            var verified = await _templateVerification.EnrichWithVerifiedTemplatesAsync(projected, req.UserPrompt, ct);
            return Ok(ApiResponse<ModelSuggestionsWithTemplatesResponse>.SuccessResponse(new ModelSuggestionsWithTemplatesResponse
            {
                Suggestions = local,
                VerifiedTemplates = verified.VerifiedTemplates
            }, "Catalog templates only (external AI not used)."));
        }

        var resp = await _client.SuggestModelsAsync(req, ct);
        var projected2 = new TransformSuggestResponse
        {
            Provider = resp.Provider,
            Summary = resp.AnalysisSummary,
            Columns = resp.DetectedSchema
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .Select(c => new ColumnProfile { Name = c.Name, InferredType = c.InferredType })
                .ToList()
        };
        var verified2 = await _templateVerification.EnrichWithVerifiedTemplatesAsync(projected2, req.UserPrompt, ct);

        return Ok(ApiResponse<ModelSuggestionsWithTemplatesResponse>.SuccessResponse(new ModelSuggestionsWithTemplatesResponse
        {
            Suggestions = resp,
            VerifiedTemplates = verified2.VerifiedTemplates
        }, "Model suggestions generated."));
    }

    /// <summary>
    /// Samples up to 100 rows from a blob (CSV/XLSX) and asks InsightsEngine.Api to propose dashboard/data models.
    /// </summary>
    [HttpPost("models/suggest-from-blob")]
    public async Task<IActionResult> SuggestModelsFromBlob([FromBody] ModelSuggestFromBlobRequest? body, CancellationToken ct)
    {
        var (client, err) = await ResolveTargetClientAsync(body?.ClientCode, body?.UseSelectedClient == true, ct);
        if (err != null) return err;
        if (client == null) return NotFound();

        var maxRows = body?.MaxRows <= 0 ? 100 : Math.Min(body!.MaxRows, 100);

        var blobPath = (body?.BlobPath ?? "").Trim();
        if (string.IsNullOrWhiteSpace(blobPath))
        {
            var folder = (client.BlobFolderPath ?? client.ClientCode ?? client.Id.ToString()).Trim();
            var prefix = $"{folder}/accounting/created/";
            blobPath = await _blobStorage.GetLatestBlobPathByPrefixAsync(prefix, ".xlsx", ct)
                ?? await _blobStorage.GetLatestBlobPathByPrefixAsync(prefix, ".csv", ct)
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(blobPath))
                return NotFound(ApiResponse<object>.ErrorResponse($"No .xlsx or .csv found under '{prefix}'."));
        }

        var sample = await _sampling.CreateSampleAsync(blobPath, client.Id, maxRows, ct);
        var element = JsonSerializer.SerializeToElement(sample.SampleRows);

        var req = new ModelSuggestRequest
        {
            ClientId = client.Id.ToString(),
            MaxRows = maxRows,
            UserPrompt = string.IsNullOrWhiteSpace(body?.UserPrompt) ? null : body!.UserPrompt!.Trim(),
            Sample = element
        };

        var columnNames = sample.Columns?.Count > 0
            ? (IReadOnlyList<string>)sample.Columns
            : InsightsSampleColumnExtractor.ExtractColumnNames(element);
        var best = await _templateMatching.GetBestCatalogMatchScoreAsync(columnNames, ct);
        var opt = _insightsEngineOptions.CurrentValue;
        if (!CopilotExternalAiGate.ShouldCallExternalInsightsEngine(opt, best))
        {
            var local = BuildCatalogOnlyModelResponse(columnNames, opt);
            var projected0 = new TransformSuggestResponse
            {
                Provider = local.Provider,
                Summary = local.AnalysisSummary,
                Columns = local.DetectedSchema
                    .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => new ColumnProfile { Name = c.Name, InferredType = c.InferredType })
                    .ToList()
            };
            var verified0 = await _templateVerification.EnrichWithVerifiedTemplatesAsync(projected0, req.UserPrompt, ct);
            return Ok(ApiResponse<ModelSuggestionsWithTemplatesResponse>.SuccessResponse(new ModelSuggestionsWithTemplatesResponse
            {
                Suggestions = local,
                VerifiedTemplates = verified0.VerifiedTemplates
            }, "Catalog templates only (external AI not used)."));
        }

        var resp = await _client.SuggestModelsAsync(req, ct);

        // Reuse our deterministic verification logic by projecting detected schema -> column profiles.
        var projected = new TransformSuggestResponse
        {
            Provider = resp.Provider,
            Summary = resp.AnalysisSummary,
            Columns = resp.DetectedSchema
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .Select(c => new ColumnProfile { Name = c.Name, InferredType = c.InferredType })
                .ToList()
        };

        var verified = await _templateVerification.EnrichWithVerifiedTemplatesAsync(projected, req.UserPrompt, ct);

        return Ok(ApiResponse<ModelSuggestionsWithTemplatesResponse>.SuccessResponse(new ModelSuggestionsWithTemplatesResponse
        {
            Suggestions = resp,
            VerifiedTemplates = verified.VerifiedTemplates
        }, "Model suggestions generated."));
    }

    /// <summary>
    /// Canonical contract: samples ≤100 rows from CSV/XLSX and returns DashboardTemplatePlan(s) for AI → UI → Validation → TMDL → PBIP.
    /// </summary>
    [HttpPost("plans/generate-from-blob")]
    public async Task<IActionResult> GeneratePlansFromBlob([FromBody] PlansGenerateFromBlobRequest? body, CancellationToken ct)
    {
        var (client, err) = await ResolveTargetClientAsync(body?.ClientCode, body?.UseSelectedClient == true, ct);
        if (err != null) return err;
        if (client == null) return NotFound();

        var maxRows = body?.MaxRows <= 0 ? 100 : Math.Min(body!.MaxRows, 100);

        var blobPath = (body?.BlobPath ?? "").Trim();
        if (string.IsNullOrWhiteSpace(blobPath))
        {
            var folder = (client.BlobFolderPath ?? client.ClientCode ?? client.Id.ToString()).Trim();
            var prefix = $"{folder}/accounting/created/";
            blobPath = await _blobStorage.GetLatestBlobPathByPrefixAsync(prefix, ".xlsx", ct)
                ?? await _blobStorage.GetLatestBlobPathByPrefixAsync(prefix, ".csv", ct)
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(blobPath))
                return NotFound(ApiResponse<object>.ErrorResponse($"No .xlsx or .csv found under '{prefix}'."));
        }

        var sample = await _sampling.CreateSampleAsync(blobPath, client.Id, maxRows, ct);
        var element = JsonSerializer.SerializeToElement(sample.SampleRows);

        var mode = (body?.Mode ?? "PredictScenarios").Trim();
        if (mode.Length == 0) mode = "PredictScenarios";

        var req = new PlansGenerateRequest
        {
            ClientId = client.Id.ToString(),
            Mode = mode,
            UserScenario = string.IsNullOrWhiteSpace(body?.UserPrompt) ? null : body!.UserPrompt!.Trim(),
            Sample = element
        };

        var columnNames = sample.Columns?.Count > 0
            ? (IReadOnlyList<string>)sample.Columns
            : InsightsSampleColumnExtractor.ExtractColumnNames(element);
        var best = await _templateMatching.GetBestCatalogMatchScoreAsync(columnNames, ct);
        var opt = _insightsEngineOptions.CurrentValue;
        if (!CopilotExternalAiGate.ShouldCallExternalInsightsEngine(opt, best))
        {
            var local = new PlansGenerateResponse
            {
                Provider = "StudioTechBI.Catalog",
                Summary = BuildCatalogOnlyPlanSummary(opt),
                Plans = new List<DashboardTemplatePlan>()
            };
            return Ok(ApiResponse<PlansGenerateResponse>.SuccessResponse(local, "Catalog only (external AI not used)."));
        }

        var resp = await _client.GeneratePlansAsync(req, ct);
        return Ok(ApiResponse<PlansGenerateResponse>.SuccessResponse(resp, "Plans generated."));
    }

    private static TransformSuggestResponse BuildCatalogOnlyTransformInsights(
        IReadOnlyList<string> columnNames,
        InsightsEngineOptions options)
    {
        return new TransformSuggestResponse
        {
            Provider = "StudioTechBI.Catalog",
            Summary = !options.ExternalCopilotAiEnabled
                ? "External copilot AI is disabled (InsightsEngine:ExternalCopilotAiEnabled=false). Showing verified dashboard templates from your column names only."
                : $"External copilot AI skipped: your data already matches a catalog report template (score threshold {options.MinTemplateMatchScoreToSkipExternalAi:0.##}).",
            Columns = columnNames.Select(n => new ColumnProfile { Name = n }).ToList()
        };
    }

    private static ModelSuggestResponse BuildCatalogOnlyModelResponse(
        IReadOnlyList<string> columnNames,
        InsightsEngineOptions options)
    {
        return new ModelSuggestResponse
        {
            Provider = "StudioTechBI.Catalog",
            AnalysisSummary = !options.ExternalCopilotAiEnabled
                ? "External copilot AI is disabled (InsightsEngine:ExternalCopilotAiEnabled=false). Proposed models are not generated; use VerifiedTemplates for catalog matches."
                : $"External copilot AI skipped: schema already aligns with a template (threshold {options.MinTemplateMatchScoreToSkipExternalAi:0.##}).",
            DetectedSchema = columnNames.Select(n => new DetectedColumn { Name = n }).ToList()
        };
    }

    private static string BuildCatalogOnlyPlanSummary(InsightsEngineOptions options) =>
        !options.ExternalCopilotAiEnabled
            ? "External copilot AI is disabled (InsightsEngine:ExternalCopilotAiEnabled=false). No remote plan generation; adjust InsightsEngine:ExternalCopilotAiEnabled to enable."
            : $"External copilot plan generation skipped: data matches a template at or above the threshold ({options.MinTemplateMatchScoreToSkipExternalAi:0.##}).";
}

