using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StudioTechBI.Application.DTOs.DashboardTemplate;
using StudioTechBI.Application.DTOs.ReportDesigner;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Utilities;

namespace StudioTechBI.Application.Services;

/// <summary>
/// Genuinely greenfield: exhaustive search this session found zero blueprint→report-visual
/// generation code anywhere in either koru-main or stbi_transformers — BLUEPRINT_TEMPLATE_FORMAT.md
/// explicitly rules it out as a non-goal for the matching-only platform it was written for. There
/// is no proven spec to build against, only Power BI's own well-documented, stable top-level report
/// layout shape (sections[]/visualContainers[]/config) and the external deploy_agent.py prototype's
/// proof that *editing* an existing report.json this way round-trips through Power BI's Import API.
///
/// Two things this deliberately does NOT claim high confidence in:
/// 1. The innermost visualContainer.config JSON (visualType/projections/objects) is a best-effort
///    approximation of Power BI Desktop's real internal shape — genuinely plausible, structurally
///    consistent, but unverified against a live import (no Power BI tenant available this session).
/// 2. v1 covers 6 visual types only (card, table, matrix, bar, line — KPI folds into card);
///    anything else falls back to a Table with a log entry rather than blocking generation.
///
/// Accepts either supported blueprint shape (detected via BlueprintFormatDetector): the
/// stbi_transformers Analytics Blueprint (data_model + abstract visual types, needs axis
/// inference) or the richer Design Blueprint (real PBI-ish type names + explicit field bindings,
/// no inference needed). Both funnel into the same normalized model before report.json emission.
/// </summary>
public sealed class ReportVisualGenerator : IReportVisualGenerator
{
    private const int CanvasWidth = 1280;
    private const int CanvasHeight = 720;
    private const int DefaultGridColumns = 3;

    private static readonly Dictionary<string, string> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["card"] = "card",
        ["kpi"] = "card",
        ["table"] = "tableEx",
        ["tableex"] = "tableEx",
        ["matrix"] = "matrix",
        ["bar"] = "barChart",
        ["barchart"] = "barChart",
        ["line"] = "lineChart",
        ["linechart"] = "lineChart",
    };

    public ReportGenerationResult Generate(
        JsonElement blueprint,
        List<BlendedTable> blendedTables,
        List<TmdlFileDto> authoredTmdlFiles,
        string reportDisplayName)
    {
        var log = new List<string>();

        var pages = BlueprintFormatDetector.IsDesignBlueprint(blueprint)
            ? AdaptDesignBlueprintPages(blueprint, log)
            : AdaptAnalyticsBlueprintPages(blueprint, log);

        ValidateReferences(pages, blendedTables, authoredTmdlFiles, log);

        var reportJson = BuildReportJson(pages);
        var platformJson = BuildPlatformManifest(reportDisplayName);

        return new ReportGenerationResult(reportJson, platformJson, log);
    }

    // ── Normalized model (shared by both adapters) ──────────────────────────────────────────

    private sealed record NormalizedVisual(string Title, string PbiType, List<string> Measures, string? CategoryField, int Row, int Col);
    private sealed record NormalizedPage(string Name, List<NormalizedVisual> Visuals);

    private static string MapVisualType(string rawType, List<string> log, string visualTitle)
    {
        if (TypeMap.TryGetValue(rawType.Trim(), out var mapped))
            return mapped;
        log.Add($"Visual '{visualTitle}': type '{rawType}' not in the v1 visual set — rendered as a Table.");
        return "tableEx";
    }

    private static bool NeedsCategoryAxis(string pbiType) => pbiType is "barChart" or "lineChart";

    // ── Adapter 1: stbi_transformers Analytics Blueprint (data_model + abstract visual types) ──

    private static List<NormalizedPage> AdaptAnalyticsBlueprintPages(JsonElement blueprint, List<string> log)
    {
        var defaultAxis = InferDefaultAxisField(blueprint, log);
        var result = new List<NormalizedPage>();

        if (!blueprint.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var page in pages.EnumerateArray())
        {
            var pageName = page.TryGetProperty("name", out var n) ? n.GetString() ?? "Page" : "Page";
            var visuals = new List<NormalizedVisual>();
            var autoIndex = 0;

            if (page.TryGetProperty("visuals", out var vs) && vs.ValueKind == JsonValueKind.Array)
            {
                foreach (var v in vs.EnumerateArray())
                {
                    var title = v.TryGetProperty("title", out var t) ? t.GetString() ?? "Visual" : "Visual";
                    var rawType = v.TryGetProperty("type", out var ty) ? ty.GetString() ?? "" : "";
                    var pbiType = MapVisualType(rawType, log, title);

                    var measures = ReadStringArray(v, "measures");

                    string? category = null;
                    if (NeedsCategoryAxis(pbiType))
                    {
                        category = defaultAxis;
                        if (category is null)
                            log.Add($"Visual '{title}': needs a category axis but no dimension table was found in the blueprint's data_model — rendered without one.");
                    }

                    var positionText = v.TryGetProperty("position", out var p) ? p.GetString() : null;
                    var (row, col) = ParsePosition(positionText, autoIndex, log, title);
                    autoIndex++;

                    visuals.Add(new NormalizedVisual(title, pbiType, measures, category, row, col));
                }
            }

            result.Add(new NormalizedPage(pageName, visuals));
        }

        return result;
    }

    /// <summary>Analytics Blueprint visuals never specify an axis field. Pick one dimension
    /// table/column once for the whole generation pass — a date-like one if any exists, else
    /// the first dimension column found — and reuse it for every chart needing one.</summary>
    private static string? InferDefaultAxisField(JsonElement blueprint, List<string> log)
    {
        if (!blueprint.TryGetProperty("data_model", out var dataModel)) return null;
        if (!dataModel.TryGetProperty("dimension_tables", out var dims) || dims.ValueKind != JsonValueKind.Array)
            return null;

        string? dateField = null;
        string? firstField = null;

        foreach (var dim in dims.EnumerateArray())
        {
            var tableName = dim.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(tableName)) continue;
            if (!dim.TryGetProperty("columns", out var cols) || cols.ValueKind != JsonValueKind.Array) continue;

            foreach (var col in cols.EnumerateArray())
            {
                var colName = col.TryGetProperty("name", out var cn) ? cn.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(colName)) continue;

                firstField ??= $"{tableName}[{colName}]";
                if (dateField is null &&
                    (tableName.Contains("date", StringComparison.OrdinalIgnoreCase) || colName.Contains("date", StringComparison.OrdinalIgnoreCase)))
                {
                    dateField = $"{tableName}[{colName}]";
                }
            }
        }

        var chosen = dateField ?? firstField;
        if (chosen is not null)
            log.Add($"Axis auto-selected: {chosen} (used for chart visuals with no explicit field in this blueprint format).");
        return chosen;
    }

    private static (int Row, int Col) ParsePosition(string? position, int autoIndex, List<string> log, string title)
    {
        if (!string.IsNullOrWhiteSpace(position))
        {
            var match = Regex.Match(position, @"Row\s*(\d+)\s*Col\s*(\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
                return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));

            log.Add($"Visual '{title}': position '{position}' could not be parsed — placed automatically.");
        }

        return (autoIndex / DefaultGridColumns + 1, autoIndex % DefaultGridColumns + 1);
    }

    // ── Adapter 2: Design Blueprint (real PBI-ish types + explicit "Table[Column]" fields) ────

    private static List<NormalizedPage> AdaptDesignBlueprintPages(JsonElement blueprint, List<string> log)
    {
        var result = new List<NormalizedPage>();
        if (!blueprint.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var page in pages.EnumerateArray())
        {
            var pageName = page.TryGetProperty("name", out var n) ? n.GetString() ?? "Page" : "Page";
            var visuals = new List<NormalizedVisual>();
            var autoIndex = 0;

            if (page.TryGetProperty("visuals", out var vs) && vs.ValueKind == JsonValueKind.Array)
            {
                foreach (var v in vs.EnumerateArray())
                {
                    var title = v.TryGetProperty("title", out var t) ? t.GetString() ?? "Visual" : "Visual";
                    var rawType = v.TryGetProperty("type", out var ty) ? ty.GetString() ?? "" : "";
                    var pbiType = MapVisualType(rawType, log, title);

                    var measures = ReadStringArray(v, "measures");

                    string? category = null;
                    if (NeedsCategoryAxis(pbiType))
                    {
                        var fields = ReadStringArray(v, "fields");
                        category = fields.Count > 0 ? fields[0] : null;
                    }

                    // Design Blueprint has no position field at all — always auto-flow.
                    var row = autoIndex / DefaultGridColumns + 1;
                    var col = autoIndex % DefaultGridColumns + 1;
                    autoIndex++;

                    visuals.Add(new NormalizedVisual(title, pbiType, measures, category, row, col));
                }
            }

            result.Add(new NormalizedPage(pageName, visuals));
        }

        return result;
    }

    private static List<string> ReadStringArray(JsonElement obj, string propertyName)
    {
        var result = new List<string>();
        if (obj.TryGetProperty(propertyName, out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } s)
                    result.Add(s);
        }
        return result;
    }

    // ── Model validation — directly answers "make sure the model is validated" ────────────────

    /// <summary>Cross-checks every measure/field a visual references against the blended dataset's
    /// real columns and the authored TMDL's own measure definitions. Never blocks generation —
    /// Power BI will just show an empty visual for an unresolved reference — but logs it as an
    /// issue the client can act on, same as the provenance/axis-inference notes.</summary>
    private static void ValidateReferences(List<NormalizedPage> pages, List<BlendedTable> blendedTables, List<TmdlFileDto> authoredTmdlFiles, List<string> log)
    {
        var blendedColumns = new HashSet<string>(
            blendedTables.SelectMany(t => t.Columns.Select(c => $"{t.TableName}[{c}]")),
            StringComparer.OrdinalIgnoreCase);

        var authoredText = string.Join("\n", authoredTmdlFiles.Select(f => f.Content));
        var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var visual in pages.SelectMany(p => p.Visuals))
        {
            foreach (var measure in visual.Measures)
            {
                if (!reported.Add(measure)) continue;
                var defined = authoredText.Contains($"measure '{measure}'", StringComparison.OrdinalIgnoreCase)
                    || authoredText.Contains($"measure {measure} ", StringComparison.OrdinalIgnoreCase)
                    || authoredText.Contains($"measure {measure}=", StringComparison.OrdinalIgnoreCase);
                if (!defined)
                    log.Add($"Measure '{measure}' is referenced by visual '{visual.Title}' but was not found in the authored semantic model — this visual may show no data until it's added.");
            }

            if (visual.CategoryField is { } field && !blendedColumns.Contains(field) && reported.Add(field))
                log.Add($"Field '{field}' used by visual '{visual.Title}' was not found in the blended dataset — this visual may show no data until that column is added.");
        }
    }

    // ── report.json / .platform emission ────────────────────────────────────────────────────

    private static string BuildReportJson(List<NormalizedPage> pages)
    {
        var sections = pages.Select((page, pageIndex) =>
        {
            var cols = page.Visuals.Count == 0 ? 1 : Math.Max(1, page.Visuals.Max(v => v.Col));
            var rows = page.Visuals.Count == 0 ? 1 : Math.Max(1, page.Visuals.Max(v => v.Row));
            var cellWidth = CanvasWidth / cols;
            var cellHeight = CanvasHeight / rows;

            var visualContainers = page.Visuals.Select((visual, visualIndex) =>
            {
                var x = (visual.Col - 1) * cellWidth;
                var y = (visual.Row - 1) * cellHeight;
                var width = Math.Max(cellWidth - 10, 40);
                var height = Math.Max(cellHeight - 10, 40);

                var projections = new Dictionary<string, object>();
                if (visual.CategoryField is not null)
                    projections["Category"] = new[] { new { queryRef = visual.CategoryField } };
                if (visual.Measures.Count > 0)
                    projections[visual.PbiType == "card" ? "Values" : "Y"] = visual.Measures.Select(m => new { queryRef = m }).ToArray();

                var escapedTitle = visual.Title.Replace("'", "''");
                var config = JsonSerializer.Serialize(new
                {
                    name = Guid.NewGuid().ToString(),
                    singleVisual = new
                    {
                        visualType = visual.PbiType,
                        projections,
                        objects = new
                        {
                            title = new object[]
                            {
                                new { properties = new { text = new { expr = new { Literal = new { Value = $"'{escapedTitle}'" } } } } }
                            }
                        }
                    }
                });

                return new { x, y, z = visualIndex, width, height, config };
            }).ToList();

            return new
            {
                name = Guid.NewGuid().ToString(),
                displayName = page.Name,
                ordinal = pageIndex,
                width = CanvasWidth,
                height = CanvasHeight,
                visualContainers,
                config = "{}"
            };
        }).ToList();

        var reportConfig = JsonSerializer.Serialize(new
        {
            version = "5.43",
            themeCollection = new { baseTheme = new { name = "CY24SU10" } }
        });

        var report = new
        {
            id = 0,
            resourcePackages = Array.Empty<object>(),
            sections,
            config = reportConfig
        };

        return JsonSerializer.Serialize(report);
    }

    /// <summary>Fixed, stable PBIP convention (not project-specific) — same shape every Fabric/PBIP
    /// project uses. Best-effort/unverified like everything else in this generator.</summary>
    private static string BuildPlatformManifest(string displayName)
    {
        var root = new JsonObject
        {
            ["$schema"] = "https://developer.microsoft.com/json-schemas/fabric/gitIntegration/platformProperties/2.0.0/schema.json",
            ["metadata"] = new JsonObject { ["type"] = "Report", ["displayName"] = displayName },
            ["config"] = new JsonObject { ["version"] = "2.0", ["logicalId"] = Guid.NewGuid().ToString() }
        };
        return root.ToJsonString();
    }
}
