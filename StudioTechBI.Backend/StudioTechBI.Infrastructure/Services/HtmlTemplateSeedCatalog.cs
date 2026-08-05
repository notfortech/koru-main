using System.Reflection;

namespace StudioTechBI.Infrastructure.Services;

/// <summary>
/// Built-in fallback for the 2 onboarded HTML report templates (retail-single-page,
/// healthcare-fpna-multi-tab). The original design assumed these would be uploaded to the
/// "clients" container at templates/html/&lt;id&gt;/{manifest.json,chrome.html} (see
/// HtmlTemplateRegistrySyncService/HtmlReportAssemblyService's own comments) — but they were
/// actually uploaded, manually, straight as flat .html files into the pre-existing
/// "report-templates" container's "Dashboard Templates" folder, alongside no manifest.json and
/// no index.json. HtmlTemplateRegistrySyncService therefore found nothing to sync, and every
/// match check against these two templates silently returned zero candidates.
///
/// Worse, those uploaded files are almost certainly the ORIGINAL example files (pre-onboarding) —
/// confirmed by size (the uploaded Healthcare_FPA_Dashboard.html is ~330 KiB vs. this repo's own
/// onboarded chrome.html at ~52 KiB) — meaning they still carry the hardcoded example dataset
/// (`const RAW = [...]`) instead of this repo's actually-onboarded version, which reads its data
/// from the injected `&lt;script id="stbi-report-data"&gt;` block (see chrome.html's own
/// `RAW_SOURCE = JSON.parse(...)` line). Fetching chrome.html from that blob location would
/// silently render the *original sample data* regardless of what the caller's real dataset says.
///
/// So this seed ships the verified, already-onboarded chrome.html for both templates as embedded
/// resources (EmbeddedTemplates/HtmlReports/*.chrome.html, see the .csproj) — a copy of exactly
/// what's in DashboardTemplateLibrary/templates/html/&lt;id&gt;/chrome.html, the local authoring
/// tree — plus each manifest's content (also copied verbatim from that same tree). Ships with the
/// assembly, so it works regardless of blob storage state. A template later uploaded properly to
/// "clients"/templates/html/&lt;id&gt;/... still takes priority over its seed entry of the same id
/// for MATCHING purposes (see HtmlTemplateRegistrySyncService) — but HtmlReportAssemblyService
/// always prefers the seed's own known-good chrome.html for these two specific ids over any blob
/// fetch, since a properly-authored "clients" copy for these ids doesn't exist anywhere yet.
/// </summary>
public static class HtmlTemplateSeedCatalog
{
    public sealed record SeedTemplate(string Id, string ManifestJson, string ChromeResourceName);

    public static readonly IReadOnlyList<SeedTemplate> Templates = new[]
    {
        new SeedTemplate(
            Id: "retail-single-page",
            ManifestJson: """
            {
              "id": "retail-single-page",
              "name": "Retail Commercial Performance (single page)",
              "industry": "retail",
              "requires": {
                "minNumeric": 3,
                "minDate": 1,
                "minCategorical": 2
              },
              "requiredColumns": [
                "Month",
                "Category",
                "Channel",
                "Budget Revenue",
                "Actual Revenue"
              ],
              "optionalColumns": [
                "Budget COGS",
                "Actual COGS",
                "Budget Units",
                "Actual Units"
              ],
              "dataContract": {
                "rowFields": [
                  { "column": "Month", "alias": "m", "role": "date" },
                  { "column": "Category", "alias": "cat", "role": "text" },
                  { "column": "Channel", "alias": "ch", "role": "text" },
                  { "column": "Budget Revenue", "alias": "bRev", "role": "numeric" },
                  { "column": "Actual Revenue", "alias": "aRev", "role": "numeric" },
                  { "column": "Budget COGS", "alias": "bCOGS", "role": "numeric" },
                  { "column": "Actual COGS", "alias": "aCOGS", "role": "numeric" },
                  { "column": "Budget Units", "alias": "bU", "role": "numeric" },
                  { "column": "Actual Units", "alias": "aU", "role": "numeric" }
                ],
                "maxRows": 5000
              },
              "testIds": {
                "resultsLoaded": "dashboard-ready",
                "kpiPrefix": "kpi-tile-",
                "chartPrefix": "chart-"
              },
              "themeSlots": {
                "primary": "--brand",
                "secondary": "--brand2",
                "accent": "--brand3",
                "background": "--bg"
              }
            }
            """,
            ChromeResourceName: "HtmlTemplates.retail-single-page.chrome.html"),

        new SeedTemplate(
            Id: "healthcare-fpna-multi-tab",
            ManifestJson: """
            {
              "id": "healthcare-fpna-multi-tab",
              "name": "Healthcare FP&A (Executive / Expenditure / Funding)",
              "industry": "healthcare",
              "requires": {
                "minNumeric": 2,
                "minDate": 1,
                "minCategorical": 3
              },
              "requiredColumns": [
                "Month",
                "Program Category",
                "State",
                "Division",
                "Budget Expenditure",
                "Actual Expenditure"
              ],
              "optionalColumns": [
                "Quarter",
                "Program Code",
                "Program Name",
                "Funding Model",
                "Cost Centre Code",
                "Cost Centre Name",
                "Is Direct Care",
                "FTE",
                "Invoices"
              ],
              "dataContract": {
                "rowFields": [
                  { "column": "Month", "alias": "month", "role": "date" },
                  { "column": "Quarter", "alias": "quarter", "role": "text" },
                  { "column": "Program Code", "alias": "progCode", "role": "text" },
                  { "column": "Program Name", "alias": "progName", "role": "text" },
                  { "column": "Program Category", "alias": "category", "role": "text" },
                  { "column": "Funding Model", "alias": "fundingModel", "role": "text" },
                  { "column": "Cost Centre Code", "alias": "ccCode", "role": "text" },
                  { "column": "Cost Centre Name", "alias": "ccName", "role": "text" },
                  { "column": "Division", "alias": "division", "role": "text" },
                  { "column": "State", "alias": "state", "role": "text" },
                  { "column": "Is Direct Care", "alias": "isDirectCare", "role": "text" },
                  { "column": "Actual Expenditure", "alias": "aExp", "role": "numeric" },
                  { "column": "Budget Expenditure", "alias": "bExp", "role": "numeric" },
                  { "column": "FTE", "alias": "fte", "role": "numeric" },
                  { "column": "Invoices", "alias": "invoices", "role": "numeric" }
                ],
                "maxRows": 5000
              },
              "testIds": {
                "resultsLoaded": "dashboard-ready",
                "kpiPrefix": "kpi-tile-",
                "chartPrefix": "chart-"
              },
              "notes": "Executive Summary and Expenditure Detail tabs are driven by real client data. The Funding & Sustainability tab needs a second, differently-shaped row table (funding by channel/funder) that the current single-table row export pipeline doesn't produce yet, so it renders with no data until that's added -- closest-match, not fully interactive, per the template library's own coverage policy.",
              "themeSlots": {
                "primary": "--brand",
                "secondary": "--brand-mid",
                "accent": "--brand-light",
                "background": "--bg"
              }
            }
            """,
            ChromeResourceName: "HtmlTemplates.healthcare-fpna-multi-tab.chrome.html"),
    };

    public static SeedTemplate? Find(string id) =>
        Templates.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Reads a seed template's embedded chrome.html, or null if the resource is somehow
    /// missing (would indicate a packaging bug, not a runtime/blob-state issue).</summary>
    public static Stream? OpenChromeHtml(SeedTemplate seed) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(seed.ChromeResourceName);
}
