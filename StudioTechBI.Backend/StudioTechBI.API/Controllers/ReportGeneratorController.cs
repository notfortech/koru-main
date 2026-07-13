using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.ReportGenerator;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers;

/// <summary>
/// Report Generator pipeline: connect to real data (Excel/CSV upload) → a
/// deterministic, no-AI engine matches a standard template and computes
/// real KPI/chart values. Unlike ReportDesignerController, this path
/// intentionally forwards the actual file — the receiving service
/// (DashboardAgents.ReportAgent.Api) has no AI/LLM call anywhere in it.
/// </summary>
[ApiController]
[Route("api/report-generator")]
[Authorize]
public class ReportGeneratorController : ControllerBase
{
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private static readonly string[] AllowedExtensions = { ".xlsx", ".csv" };

    private readonly IReportGeneratorClient _reportGeneratorClient;

    public ReportGeneratorController(IReportGeneratorClient reportGeneratorClient)
    {
        _reportGeneratorClient = reportGeneratorClient;
    }

    /// <summary>
    /// GET /api/report-generator/templates
    /// Lists the standard-template registry (metadata only — id, name,
    /// industry, required column shape). No data is involved in this call.
    /// </summary>
    [HttpGet("templates")]
    public async Task<IActionResult> ListTemplatesAsync(CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString();
        try
        {
            var templates = await _reportGeneratorClient.ListTemplatesAsync(correlationId, cancellationToken);
            return Ok(ApiResponse<List<ReportTemplateDto>>.SuccessResponse(templates, "Templates retrieved successfully."));
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(
                (int)(ex.StatusCode ?? System.Net.HttpStatusCode.BadGateway),
                ApiResponse<object>.ErrorResponse(ex.Message));
        }
    }

    /// <summary>
    /// POST /api/report-generator/generate
    /// Accepts an Excel/CSV upload and an optional templateId (defaults to
    /// the engine's best rule-based match). Returns real, computed KPI/chart
    /// values — no AI touches this file at any point in the pipeline.
    /// </summary>
    [HttpPost("generate")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<IActionResult> GenerateAsync(
        IFormFile file,
        [FromForm] string? templateId,
        [FromForm] string? filters,
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

        var correlationId = Guid.NewGuid().ToString();

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _reportGeneratorClient.GenerateReportAsync(
                stream, file.FileName, templateId, filters, correlationId, cancellationToken);

            return Ok(ApiResponse<GeneratedReportDto>.SuccessResponse(result, "Report generated successfully."));
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
    }
}
