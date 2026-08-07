using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.Constants;
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
    private const string StyleCloseTag = "</style>";

    // Matches a template author's own pre-authored `id="stbi-report-data"` element (e.g. an empty
    // placeholder they intended to fill in themselves), whole tag through its closing </script>.
    private static readonly Regex ExistingDataScriptRegex = new(
        "<script\\b[^>]*\\bid\\s*=\\s*[\"']stbi-report-data[\"'][^>]*>[\\s\\S]*?</script\\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

        // Blob is always tried first, regardless of whether this id also has a built-in seed
        // (HtmlTemplateSeedCatalog) -- the master "clients"/templates/html/<id>/ blob copy is the
        // source of truth once it exists, so an author can update a template by uploading a new
        // blob, with zero koru-main code change or redeploy. The seed catalog is only a
        // fallback-of-last-resort (a cold/fresh environment that hasn't synced yet, or a
        // transient blob outage) -- it must never permanently shadow a correctly-uploaded
        // template the way an unconditional seed-first check would.
        string? chromeHtml = await TryDownloadTextAsync(
            $"{TemplatesBasePath}/{report.HtmlTemplateId}/{ChromeFileName}", cancellationToken);
        string? manifestJson = chromeHtml is null
            ? null
            : await TryDownloadTextAsync(
                $"{TemplatesBasePath}/{report.HtmlTemplateId}/{ManifestFileName}", cancellationToken);

        if (chromeHtml is null)
        {
            var seed = HtmlTemplateSeedCatalog.Find(report.HtmlTemplateId);
            if (seed is null)
            {
                _logger.LogWarning(
                    "HtmlReportAssembly.ChromeNotFound TemplateId={TemplateId} — no blob copy and no seed fallback " +
                    "found; falling back to the existing KPI/chart rendering for this report.",
                    report.HtmlTemplateId);
                return report;
            }

            await using var seedStream = HtmlTemplateSeedCatalog.OpenChromeHtml(seed);
            if (seedStream is null)
            {
                _logger.LogWarning(
                    "HtmlReportAssembly.SeedResourceMissing TemplateId={TemplateId} Resource={Resource} " +
                    "— falling back to the existing KPI/chart rendering for this report.",
                    report.HtmlTemplateId, seed.ChromeResourceName);
                return report;
            }

            _logger.LogInformation(
                "HtmlReportAssembly.SeedFallback TemplateId={TemplateId} — no blob copy found, serving the " +
                "built-in seed template instead.", report.HtmlTemplateId);

            using var seedReader = new StreamReader(seedStream, Encoding.UTF8);
            chromeHtml = await seedReader.ReadToEndAsync(cancellationToken);
            manifestJson = seed.ManifestJson;
        }

        chromeHtml = ApplyThemeOverride(report.HtmlTemplateId, chromeHtml, manifestJson, themeOverride);

        // report.RowData is a raw passthrough of whatever shape the Python engine's row_export
        // module produced (see row_export.py's module docstring): a bare array for a single-table
        // manifest (`RAW_SOURCE = JSON.parse(...); RAW = RAW_SOURCE.map(...)` — confirmed against
        // both original onboarded chrome.html files, neither of which reads kpis/appliedFilters/
        // templateName from this payload at all), or an object keyed by table alias for a
        // multi-table manifest (`RAW.someTableAlias`) — koru-main never needs to know or care
        // which one it is, it's byte-identical to what Python already emitted. Falls back to an
        // empty array (not an object) when absent, matching every existing single-table
        // template's own `.map()`-on-root expectation.
        var json = report.RowData?.GetRawText() ?? "[]";

        // <script type="application/json"> is never executed by the browser, so a </script>
        // substring inside a value can't itself break out into active markup — but the HTML
        // tokenizer's "script data" parsing state still terminates the tag on the literal
        // sequence "</script" regardless of the type attribute, so that one sequence still needs
        // escaping. HTML-entity-encoding the whole payload would be wrong here (and is
        // deliberately NOT done): script-element text content isn't entity-decoded by the
        // browser, so JSON.parse(el.textContent) on entity-encoded JSON would fail/corrupt data.
        var scriptSafeJson = json.Replace("</", "<\\/", StringComparison.Ordinal);
        var scriptBlock = $"<script type=\"application/json\" id=\"stbi-report-data\">{scriptSafeJson}</script>";

        string html;
        if (chromeHtml.Contains(HtmlTemplateBlobPaths.DataMarker, StringComparison.Ordinal))
        {
            html = chromeHtml.Replace(HtmlTemplateBlobPaths.DataMarker, scriptBlock);
        }
        else if (ExistingDataScriptRegex.IsMatch(chromeHtml))
        {
            // The marker itself is missing, but the template already declares its own
            // id="stbi-report-data" element (typically an empty placeholder the author expected to
            // fill in some other way). Appending a second element with the same id -- the old
            // fallback below -- would silently lose: getElementById returns the FIRST match in
            // document order, which is this original, still-empty one, not the data we just
            // computed. Replace that element in place instead, so whichever one the template's own
            // script finds is the real data. (Confirmed this is exactly what was happening to a
            // real matched template in production: it always rendered with zero rows despite a
            // confident match, because the append-at-</body> fallback lost to the template's own
            // placeholder every time.)
            _logger.LogWarning(
                "HtmlReportAssembly.MarkerMissingReplacedExistingElement TemplateId={TemplateId} — no " +
                "{Marker} marker found, but replaced the template's own pre-existing #stbi-report-data " +
                "element in place.", report.HtmlTemplateId, HtmlTemplateBlobPaths.DataMarker);

            html = ExistingDataScriptRegex.Replace(chromeHtml, _ => scriptBlock, 1);
        }
        else
        {
            // Fail soft — a template-authoring mistake (forgetting the marker entirely, with no
            // element to replace either) must never break report generation, just degrade to
            // appending the data block right before </body>.
            _logger.LogWarning(
                "HtmlReportAssembly.MarkerMissing TemplateId={TemplateId} — appending the data block " +
                "before </body> instead of substituting the {Marker} marker.",
                report.HtmlTemplateId, HtmlTemplateBlobPaths.DataMarker);

            var bodyCloseIndex = chromeHtml.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            html = bodyCloseIndex >= 0
                ? chromeHtml.Insert(bodyCloseIndex, scriptBlock)
                : chromeHtml + scriptBlock;
        }

        return report with { HtmlReport = html };
    }

    public async Task<string?> GetManifestJsonAsync(string templateId, CancellationToken cancellationToken = default)
    {
        var manifestJson = await TryDownloadTextAsync(
            $"{TemplatesBasePath}/{templateId}/{ManifestFileName}", cancellationToken);
        if (manifestJson is not null)
            return manifestJson;

        var seed = HtmlTemplateSeedCatalog.Find(templateId);
        return seed?.ManifestJson;
    }

    /// <summary>Downloads and reads a blob as UTF-8 text, or null if it doesn't exist / can't be
    /// read -- callers treat a null result as "try the next source" (the seed fallback), never as
    /// a hard failure.</summary>
    private async Task<string?> TryDownloadTextAsync(string blobPath, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await _blobStorage.DownloadBlobAsync(blobPath, cancellationToken);
            if (stream is null)
                return null;

            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HtmlReportAssembly.BlobFetchFailed Path={Path}", blobPath);
            return null;
        }
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
