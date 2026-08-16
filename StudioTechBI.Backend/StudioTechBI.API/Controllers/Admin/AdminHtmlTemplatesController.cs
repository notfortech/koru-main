using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.Constants;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers.Admin;

/// <summary>
/// Admin content management for interactive HTML report templates -- bulk zip upload, list,
/// manifest-only editing, retirement, and an on-demand sync trigger. See
/// IHtmlTemplateAdminService for the "no code changes to add/edit a template" design this exists
/// to deliver.
/// </summary>
[ApiController]
[Route("api/admin/html-templates")]
[Authorize(Roles = "Admin,SuperAdmin,OperationsAdmin,SupportAdmin")]
public class AdminHtmlTemplatesController : ControllerBase
{
    private readonly IHtmlTemplateAdminService _service;

    public AdminHtmlTemplatesController(IHtmlTemplateAdminService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<HtmlTemplateSummaryDto>>> GetAll(CancellationToken cancellationToken)
    {
        var list = await _service.ListAsync(cancellationToken);
        return Ok(list);
    }

    [HttpPost("upload-batch")]
    [RequestSizeLimit(UploadLimits.MaxHtmlTemplateBatchBytes)]
    public async Task<ActionResult<HtmlTemplateBulkUploadResponseDto>> UploadBatch(IFormFile? zip, [FromForm] bool dryRun, CancellationToken cancellationToken)
    {
        if (zip is null || zip.Length == 0)
            return BadRequest("A .zip file is required.");

        await using var stream = zip.OpenReadStream();
        var result = await _service.UploadBatchAsync(stream, dryRun, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Replaces just one existing template's chrome.html -- the single-file complement to the
    /// manifest-only editor below. Fails (422) if the id isn't already listed, so a brand new
    /// template still has to come in via a full .zip.
    /// </summary>
    [HttpPost("{id}/chrome-html")]
    [RequestSizeLimit(UploadLimits.MaxHtmlTemplateSingleFileBytes)]
    public async Task<ActionResult<HtmlTemplateUploadResultDto>> UploadChromeHtml(string id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest("An .html file is required.");

        string html;
        await using (var stream = file.OpenReadStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            html = await reader.ReadToEndAsync(cancellationToken);
        }

        var result = await _service.UploadChromeHtmlAsync(id, html, cancellationToken);
        if (result.Status == "Failed")
            return UnprocessableEntity(result);

        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var removed = await _service.DeleteAsync(id, cancellationToken);
        if (!removed)
            return NotFound(new { message = $"Template '{id}' is not listed — it may already have been deleted." });

        return NoContent();
    }

    [HttpPost("sync-now")]
    public async Task<ActionResult<object>> SyncNow(CancellationToken cancellationToken)
    {
        var count = await _service.ForceSyncNowAsync(cancellationToken);
        return Ok(new { syncedCount = count });
    }

    [HttpGet("{id}/manifest")]
    public async Task<ActionResult<HtmlTemplateManifestResponseDto>> GetManifest(string id, CancellationToken cancellationToken)
    {
        var raw = await _service.GetManifestRawAsync(id, cancellationToken);
        if (raw is null)
            return NotFound();

        return Ok(new HtmlTemplateManifestResponseDto(id, raw));
    }

    [HttpPut("{id}/manifest")]
    public async Task<ActionResult<HtmlTemplateUploadResultDto>> UpdateManifest(
        string id, [FromBody] HtmlTemplateManifestUpdateRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateManifestAsync(id, dto.ManifestJson, cancellationToken);
        if (result.Status == "Failed")
            return UnprocessableEntity(result);

        return Ok(result);
    }

    /// <summary>
    /// Accepts the real xlsx/csv a template's dashboard was built from, profiles its columns via
    /// the same profiler the deterministic matcher's role gate runs at real match time, and returns
    /// a proposed, merged manifest.json -- unsaved. The frontend pre-fills the existing manifest
    /// editor with it; saving still goes through PUT .../manifest unchanged. The file itself is
    /// stored immediately regardless (also backs the Preview action below).
    /// </summary>
    [HttpPost("{id}/reference-dataset")]
    [RequestSizeLimit(UploadLimits.MaxUploadBytes)]
    public async Task<ActionResult<HtmlTemplateReferenceDatasetDeriveResponseDto>> UploadReferenceDataset(
        string id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest("An .xlsx or .csv file is required.");

        await using var stream = file.OpenReadStream();
        var result = await _service.DeriveManifestFromReferenceDatasetAsync(id, stream, file.FileName, cancellationToken);
        if (!result.Success)
            return UnprocessableEntity(result);

        return Ok(result);
    }

    /// <summary>
    /// Re-runs the deterministic match+render pipeline against this template's stored reference
    /// dataset and asserts it resolves back to this same template -- the admin-facing proof that a
    /// client's matching file will actually be picked up by the no-AI path.
    /// </summary>
    [HttpPost("{id}/preview")]
    public async Task<ActionResult<HtmlTemplatePreviewResponseDto>> Preview(string id, CancellationToken cancellationToken)
    {
        var result = await _service.PreviewWithReferenceDatasetAsync(id, cancellationToken);
        if (!result.Matched)
            return UnprocessableEntity(result);

        return Ok(result);
    }
}
