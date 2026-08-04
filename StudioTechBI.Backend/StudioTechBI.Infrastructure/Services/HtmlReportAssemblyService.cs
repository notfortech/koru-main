using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.ReportGenerator;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Services;

/// <summary>
/// Reads a matched HTML report template's chrome.html straight off the master blob path
/// (read-only, in-memory, every call — see IHtmlReportAssemblyService remarks) and injects the
/// already-computed report data into it via marker substitution. Mirrors the existing
/// TmdlSourcePatcher "patch a known marker in a static authored file" pattern already used
/// elsewhere in this codebase.
/// </summary>
public class HtmlReportAssemblyService : IHtmlReportAssemblyService
{
    // Same container/prefix Power BI report templates already live in — see
    // BlobStorageService.UploadTemplateAsync ("templates/{industry}/{version}/{fileName}").
    // HTML templates nest as a sibling prefix within that same tree: templates/html/<id>/.
    private const string TemplatesBasePath = "templates/html";
    private const string ChromeFileName = "chrome.html";
    private const string DataMarker = "<!--STBI_REPORT_DATA-->";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<HtmlReportAssemblyService> _logger;

    public HtmlReportAssemblyService(IBlobStorageService blobStorage, ILogger<HtmlReportAssemblyService> logger)
    {
        _blobStorage = blobStorage;
        _logger = logger;
    }

    public async Task<GeneratedReportDto> AssembleAsync(GeneratedReportDto report, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(report.HtmlTemplateId))
            return report;

        // A seed-catalog template's chrome.html ships as an embedded resource with this assembly
        // (see HtmlTemplateSeedCatalog) rather than living under this service's default
        // "clients"/templates/html/<id>/ blob convention below.
        var seed = HtmlTemplateSeedCatalog.Find(report.HtmlTemplateId);

        string chromeHtml;
        if (seed is not null)
        {
            await using var seedStream = HtmlTemplateSeedCatalog.OpenChromeHtml(seed);
            if (seedStream is null)
            {
                _logger.LogWarning(
                    "HtmlReportAssembly.SeedResourceMissing TemplateId={TemplateId} Resource={Resource} " +
                    "— falling back to the existing KPI/chart rendering for this report.",
                    report.HtmlTemplateId, seed.ChromeResourceName);
                return report;
            }

            using var seedReader = new StreamReader(seedStream, Encoding.UTF8);
            chromeHtml = await seedReader.ReadToEndAsync(cancellationToken);
        }
        else
        {
            var chromePath = $"{TemplatesBasePath}/{report.HtmlTemplateId}/{ChromeFileName}";

            await using var chromeStream = await _blobStorage.DownloadBlobAsync(chromePath, cancellationToken);
            if (chromeStream is null)
            {
                _logger.LogWarning(
                    "HtmlReportAssembly.ChromeNotFound TemplateId={TemplateId} Path={Path} — falling back to the " +
                    "existing KPI/chart rendering for this report.",
                    report.HtmlTemplateId, chromePath);
                return report;
            }

            using var reader = new StreamReader(chromeStream, Encoding.UTF8);
            chromeHtml = await reader.ReadToEndAsync(cancellationToken);
        }

        // The chrome's own JS parses this script block's content as the bare row array itself
        // (`RAW_SOURCE = JSON.parse(...); RAW = RAW_SOURCE.map(...)` — confirmed against both
        // onboarded chrome.html files, neither of which reads kpis/appliedFilters/templateName
        // from this payload at all; every KPI/chart/filter is computed client-side from the row
        // data). Injecting a {kpis,charts,rowData,...} wrapper object here instead of the bare
        // array throws immediately on that first .map() call (a plain object has no .map), which
        // silently aborts the rest of the inline script — the exact "chrome renders, nothing
        // populates" symptom this replaced.
        var rowData = report.RowData ?? new List<Dictionary<string, object?>>();

        // <script type="application/json"> is never executed by the browser, so a </script>
        // substring inside a value can't itself break out into active markup — but the HTML
        // tokenizer's "script data" parsing state still terminates the tag on the literal
        // sequence "</script" regardless of the type attribute, so that one sequence still needs
        // escaping. HTML-entity-encoding the whole payload would be wrong here (and is
        // deliberately NOT done): script-element text content isn't entity-decoded by the
        // browser, so JSON.parse(el.textContent) on entity-encoded JSON would fail/corrupt data.
        var json = JsonSerializer.Serialize(rowData, JsonOptions);
        var scriptSafeJson = json.Replace("</", "<\\/", StringComparison.Ordinal);
        var scriptBlock = $"<script type=\"application/json\" id=\"stbi-report-data\">{scriptSafeJson}</script>";

        string html;
        if (chromeHtml.Contains(DataMarker, StringComparison.Ordinal))
        {
            html = chromeHtml.Replace(DataMarker, scriptBlock);
        }
        else
        {
            // Fail soft — a template-authoring mistake (forgetting the marker) must never break
            // report generation, just degrade to appending the data block right before </body>.
            _logger.LogWarning(
                "HtmlReportAssembly.MarkerMissing TemplateId={TemplateId} — appending the data block " +
                "before </body> instead of substituting the {Marker} marker.",
                report.HtmlTemplateId, DataMarker);

            var bodyCloseIndex = chromeHtml.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            html = bodyCloseIndex >= 0
                ? chromeHtml.Insert(bodyCloseIndex, scriptBlock)
                : chromeHtml + scriptBlock;
        }

        return report with { HtmlReport = html };
    }
}
