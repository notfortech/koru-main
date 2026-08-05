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
    private const string ManifestFileName = "manifest.json";
    private const string DataMarker = "<!--STBI_REPORT_DATA-->";
    private const string StyleCloseTag = "</style>";

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

    public async Task<GeneratedReportDto> AssembleAsync(
        GeneratedReportDto report,
        ReportThemeOverride? themeOverride = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(report.HtmlTemplateId))
            return report;

        // A seed-catalog template's chrome.html ships as an embedded resource with this assembly
        // (see HtmlTemplateSeedCatalog) rather than living under this service's default
        // "clients"/templates/html/<id>/ blob convention below.
        var seed = HtmlTemplateSeedCatalog.Find(report.HtmlTemplateId);

        string chromeHtml;
        string? manifestJson;
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
            manifestJson = seed.ManifestJson;
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

            // Manifest is only needed for theming (below) -- a missing/unreadable one just means
            // no theme override happens, it must never block rendering the chrome itself.
            var manifestPath = $"{TemplatesBasePath}/{report.HtmlTemplateId}/{ManifestFileName}";
            try
            {
                await using var manifestStream = await _blobStorage.DownloadBlobAsync(manifestPath, cancellationToken);
                if (manifestStream is null)
                {
                    manifestJson = null;
                }
                else
                {
                    using var manifestReader = new StreamReader(manifestStream, Encoding.UTF8);
                    manifestJson = await manifestReader.ReadToEndAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "HtmlReportAssembly.ManifestFetchFailed TemplateId={TemplateId} Path={Path} — theme override " +
                    "skipped for this report, chrome rendering is unaffected.",
                    report.HtmlTemplateId, manifestPath);
                manifestJson = null;
            }
        }

        chromeHtml = ApplyThemeOverride(report.HtmlTemplateId, chromeHtml, manifestJson, themeOverride);

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

    // Two-level indirection: VisualTheme's 4 hex fields map onto 4 fixed semantic slot names in
    // code (this method); each template's manifest.json maps those same slot names onto its own
    // CSS variable names (themeSlots) -- the frontend never needs to know per-template variable
    // naming, and templates never need to know about VisualTheme's shape. A missing themeOverride,
    // a manifest with no themeSlots, an unmapped slot, or a themeSlots value naming a CSS variable
    // the template doesn't actually declare are all silent no-ops -- CSS ignores an unused custom
    // property, and this method never blocks or corrupts chrome rendering over a theming problem.
    private string ApplyThemeOverride(string templateId, string chromeHtml, string? manifestJson, ReportThemeOverride? themeOverride)
    {
        if (themeOverride is null || manifestJson is null)
            return chromeHtml;

        if (themeOverride is { Primary: null, Dark: null, Light: null, Bg: null })
            return chromeHtml;

        Dictionary<string, string>? themeSlots;
        try
        {
            using var doc = JsonDocument.Parse(manifestJson);
            if (!doc.RootElement.TryGetProperty("themeSlots", out var slotsElement))
                return chromeHtml;

            themeSlots = JsonSerializer.Deserialize<Dictionary<string, string>>(slotsElement.GetRawText());
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "HtmlReportAssembly.ManifestParseFailed TemplateId={TemplateId} — theme override skipped.",
                templateId);
            return chromeHtml;
        }

        if (themeSlots is null || themeSlots.Count == 0)
            return chromeHtml;

        var slotValues = new (string Slot, string? Hex)[]
        {
            ("primary", themeOverride.Primary),
            ("secondary", themeOverride.Dark),
            ("accent", themeOverride.Light),
            ("background", themeOverride.Bg),
        };

        var declarations = slotValues
            .Where(s => s.Hex is not null && themeSlots.ContainsKey(s.Slot))
            .Select(s => $"{themeSlots[s.Slot]}:{s.Hex}")
            .ToList();

        if (declarations.Count == 0)
            return chromeHtml;

        var styleCloseIndex = chromeHtml.IndexOf(StyleCloseTag, StringComparison.OrdinalIgnoreCase);
        if (styleCloseIndex < 0)
        {
            _logger.LogWarning(
                "HtmlReportAssembly.StyleTagMissing TemplateId={TemplateId} — no </style> found, theme override skipped.",
                templateId);
            return chromeHtml;
        }

        // Inserted right after the template's own first </style> closes -- strictly after its
        // original :root{} in source order, so the CSS cascade (later declaration of equal
        // specificity wins) makes this override win without ever touching that original block.
        // Anchoring on </style> rather than </head> matters: retail-single-page is a bare
        // fragment with no <head>/<html>/<body> tags at all, so a </head>-relative insert would
        // silently skip theming for it specifically while appearing to work for healthcare.
        var themeStyle = $"<style id=\"stbi-theme-override\">:root{{{string.Join(";", declarations)}}}</style>";
        return chromeHtml.Insert(styleCloseIndex + StyleCloseTag.Length, themeStyle);
    }
}
