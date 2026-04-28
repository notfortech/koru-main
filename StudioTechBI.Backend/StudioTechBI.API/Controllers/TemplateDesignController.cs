using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Infrastructure.Data;
using System.Text;

namespace StudioTechBI.API.Controllers;

[ApiController]
[Route("api/templates")]
[Authorize]
public sealed class TemplateDesignController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IReportTemplateAssetService _assets;

    public TemplateDesignController(ApplicationDbContext db, IReportTemplateAssetService assets)
    {
        _db = db;
        _assets = assets;
    }

    /// <summary>
    /// Returns a screenshot image for a template using blob naming convention:
    /// <c>{TemplateName}_Template_v1.jpg</c> in the <c>report-templates</c> container.
    /// </summary>
    [HttpGet("{templateId:guid}/design-image")]
    public async Task<IActionResult> GetDesignImage(Guid templateId, CancellationToken ct)
    {
        if (templateId == Guid.Empty)
            return BadRequest(new { success = false, message = "templateId is required." });

        var tpl = await _db.Templates
            .AsNoTracking()
            .Where(t => !t.IsDeleted && t.Id == templateId)
            .Select(t => new { t.Id, t.TemplateName })
            .FirstOrDefaultAsync(ct);

        if (tpl == null || string.IsNullOrWhiteSpace(tpl.TemplateName))
            return NotFound(new { success = false, message = "Template not found." });

        var blobName = BuildScreenshotBlobName(tpl.TemplateName);
        var dl = await _assets.DownloadTemplateScreenshotAsync(blobName, ct);
        if (dl == null)
            return NotFound(new { success = false, message = "Template design image not found." });

        return File(dl.Value.stream, dl.Value.contentType);
    }

    private static string BuildScreenshotBlobName(string templateName)
    {
        // e.g. "Realtor" -> "Realtor_Template_v1.jpg"
        // Normalize to a safe blob base without spaces.
        var s = (templateName ?? "").Trim();
        if (s.Length == 0) return "Template_Template_v1.jpg";

        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                sb.Append(c);
            else if (char.IsWhiteSpace(c))
                sb.Append('_');
        }
        var safe = sb.ToString().Trim('_');
        if (safe.Length == 0) safe = "Template";

        return $"{safe}_Template_v1.jpg";
    }
}

