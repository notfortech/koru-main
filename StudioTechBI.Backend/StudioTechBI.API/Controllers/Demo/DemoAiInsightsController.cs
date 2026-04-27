using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.Templates;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.API.Controllers.Demo;

[ApiController]
[Route("api/demo")]
public sealed class DemoAiInsightsController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    private readonly IInsightsEngineTemplateMappingClient _insightsEngine;

    public DemoAiInsightsController(ApplicationDbContext db, IInsightsEngineTemplateMappingClient insightsEngine)
    {
        _db = db;
        _insightsEngine = insightsEngine;
    }

    public sealed class DemoAiInsightsRequest
    {
        public List<string> Columns { get; set; } = new();
        public JsonElement SampleRows { get; set; }
    }

    public sealed class DemoTemplateInfo
    {
        public Guid TemplateId { get; set; }
        public List<string> RequiredColumns { get; set; } = new();
    }

    public sealed class TomPreview
    {
        public List<TomTable> Tables { get; set; } = new();
    }

    public sealed class TomTable
    {
        public string Name { get; set; } = "Fact";
        public List<TomColumn> Columns { get; set; } = new();
    }

    public sealed class TomColumn
    {
        public string Name { get; set; } = "";
        public string SourceColumn { get; set; } = "";
        public string? Transform { get; set; }
    }

    public sealed class DemoAiInsightsResponse
    {
        public IReadOnlyList<DemoTemplateInfo> Templates { get; set; } = Array.Empty<DemoTemplateInfo>();
        public TemplateMappingPreview Mapping { get; set; } = new();
        public IReadOnlyList<string> Transformations { get; set; } = Array.Empty<string>();
        public TomPreview TomPreview { get; set; } = new();
    }

    /// <summary>
    /// Demo-only endpoint: accepts UI-provided columns + sample rows and returns AI mapping + TOM preview.
    /// No blobs, no pipeline, no storage, no DB writes.
    /// </summary>
    [HttpPost("ai-insights")]
    [AllowAnonymous]
    public async Task<IActionResult> DemoAiInsights([FromBody] DemoAiInsightsRequest body, CancellationToken ct)
    {
        var columns = NormalizeColumns(body?.Columns);
        if (columns.Count == 0)
            return BadRequest(ApiResponse<object>.ErrorResponse("columns is required."));

        // Read templates from DB (fast, no tracking). Only what we need.
        var templates = await _db.Templates
            .AsNoTracking()
            .Where(t => !t.IsDeleted)
            .Select(t => new
            {
                t.Id,
                t.RequiredColumnsJson,
                t.OptionalColumnsJson
            })
            .ToListAsync(ct);

        var templateInfos = templates
            .Select(t => new
            {
                TemplateId = t.Id,
                Required = DeserializeColumns(t.RequiredColumnsJson),
                Optional = DeserializeColumns(t.OptionalColumnsJson)
            })
            .ToList();

        var responseTemplates = templateInfos
            .Select(t => new DemoTemplateInfo { TemplateId = t.TemplateId, RequiredColumns = t.Required })
            .ToList();

        // Pick best template deterministically (fast) to keep AI call bounded.
        var best = templateInfos
            .Select(t => new { tpl = t, score = Score(columns, t.Required, t.Optional) })
            .OrderByDescending(x => x.score)
            .FirstOrDefault();

        var mapping = new TemplateMappingPreview();
        if (best != null && (best.tpl.Required.Count > 0 || best.tpl.Optional.Count > 0))
        {
            // Best-effort AI refinement with strict time budget, fallback to deterministic mapping.
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budget.CancelAfter(TimeSpan.FromMilliseconds(1500));

            try
            {
                mapping = await _insightsEngine.RefineTemplateMappingAsync(
                    columns,
                    best.tpl.Required,
                    best.tpl.Optional,
                    budget.Token);
            }
            catch
            {
                mapping = DeterministicMapping(columns, best.tpl.Required, best.tpl.Optional);
            }
        }
        else
        {
            mapping = DeterministicMapping(columns, Array.Empty<string>(), Array.Empty<string>());
        }

        var tom = BuildTomPreview(columns, mapping);

        var result = new DemoAiInsightsResponse
        {
            Templates = responseTemplates,
            Mapping = mapping,
            Transformations = mapping.Transformations,
            TomPreview = tom
        };

        return Ok(ApiResponse<DemoAiInsightsResponse>.SuccessResponse(result, "OK"));
    }

    private static List<string> NormalizeColumns(IEnumerable<string>? columns)
    {
        if (columns == null) return new List<string>();
        return columns
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(500)
            .ToList();
    }

    private static List<string> DeserializeColumns(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static double Score(IReadOnlyList<string> clientColumns, IReadOnlyList<string> required, IReadOnlyList<string> optional)
    {
        var client = new HashSet<string>(clientColumns.Select(NormalizeKey), StringComparer.OrdinalIgnoreCase);
        var requiredHits = required.Count(c => client.Contains(NormalizeKey(c)));
        var optionalHits = optional.Count(c => client.Contains(NormalizeKey(c)));

        var requiredRatio = required.Count == 0 ? 0 : (double)requiredHits / required.Count;
        var optionalRatio = optional.Count == 0 ? 0 : (double)optionalHits / optional.Count;
        return 0.8 * requiredRatio + 0.2 * optionalRatio;
    }

    private static TemplateMappingPreview DeterministicMapping(
        IReadOnlyList<string> clientColumns,
        IReadOnlyList<string> required,
        IReadOnlyList<string> optional)
    {
        var mapping = new List<TemplateColumnMapping>();
        foreach (var col in required.Concat(optional).Distinct(StringComparer.OrdinalIgnoreCase).Take(200))
        {
            var match = clientColumns.FirstOrDefault(cc =>
                string.Equals(NormalizeKey(cc), NormalizeKey(col), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match))
                mapping.Add(new TemplateColumnMapping { TemplateColumn = col, ClientColumn = match });
        }
        return new TemplateMappingPreview { Mapping = mapping, Transformations = Array.Empty<string>(), Notes = "fallback_deterministic" };
    }

    private static TomPreview BuildTomPreview(IReadOnlyList<string> clientColumns, TemplateMappingPreview mapping)
    {
        // If we have mappings, use mapped client columns; else identity map.
        var mapped = mapping.Mapping.Count > 0
            ? mapping.Mapping
            : clientColumns.Select(c => new TemplateColumnMapping { TemplateColumn = c, ClientColumn = c }).ToList();

        var cols = mapped
            .Take(200)
            .Select(m => new TomColumn
            {
                Name = m.TemplateColumn,
                SourceColumn = m.ClientColumn,
                Transform = m.Transform
            })
            .ToList();

        return new TomPreview
        {
            Tables = new List<TomTable>
            {
                new TomTable { Name = "Fact", Columns = cols }
            }
        };
    }

    private static string NormalizeKey(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        }
        return sb.ToString();
    }
}

