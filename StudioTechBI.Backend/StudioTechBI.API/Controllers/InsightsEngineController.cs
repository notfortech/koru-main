using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.InsightsEngine;
using StudioTechBI.Application.Services;
using StudioTechBI.Infrastructure.Clients;

namespace StudioTechBI.API.Controllers;

[ApiController]
[Route("api/insights-engine")]
[Authorize]
public sealed class InsightsEngineController : ControllerBase
{
    private readonly InsightsEngineClient _client;
    private readonly DataSamplingService _sampling;

    public InsightsEngineController(InsightsEngineClient client, DataSamplingService sampling)
    {
        _client = client;
        _sampling = sampling;
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
        return Ok(ApiResponse<TransformSuggestResponse>.SuccessResponse(resp, "Suggestions generated."));
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
        return Ok(ApiResponse<TransformSuggestResponse>.SuccessResponse(resp, "Suggestions generated."));
    }
}

