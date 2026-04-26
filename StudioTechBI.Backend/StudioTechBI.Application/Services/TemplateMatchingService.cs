using System.Text;
using StudioTechBI.Application.DTOs.Templates;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Application.Services;

public sealed class TemplateMatchingService : ITemplateMatchingService
{
    private readonly ITemplateService _templates;
    private readonly DataSamplingService _sampling;
    private readonly IBlobStorageService _blobStorage;
    private readonly IClientResolver _clientResolver;
    private readonly IClientService _clientService;
    private readonly IInsightsEngineTemplateMappingClient _insightsEngine;

    public TemplateMatchingService(
        ITemplateService templates,
        DataSamplingService sampling,
        IBlobStorageService blobStorage,
        IClientResolver clientResolver,
        IClientService clientService,
        IInsightsEngineTemplateMappingClient insightsEngine)
    {
        _templates = templates;
        _sampling = sampling;
        _blobStorage = blobStorage;
        _clientResolver = clientResolver;
        _clientService = clientService;
        _insightsEngine = insightsEngine;
    }

    public async Task<TemplateMatchResponse> MatchFromBlobAsync(
        string? clientCodeOrId,
        bool useSelectedClient,
        string? blobPath,
        int maxRows,
        bool useAiRefinement,
        CancellationToken cancellationToken = default)
    {
        // Resolve client by code/id.
        // For "useSelectedClient" the controller will pass the claim-derived code.
        var resolvedClientCode = (clientCodeOrId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(resolvedClientCode))
            throw new InvalidOperationException("clientCode is required.");

        var client = await _clientResolver.ResolveAsync(resolvedClientCode, cancellationToken);
        if (client == null)
            throw new InvalidOperationException("Client not found.");

        var folder = (client.BlobFolderPath ?? client.ClientCode ?? client.Id.ToString()).Trim();
        var prefix = $"{folder}/accounting/created/";

        var resolvedBlobPath = (blobPath ?? "").Trim();
        if (string.IsNullOrWhiteSpace(resolvedBlobPath))
        {
            resolvedBlobPath = await _blobStorage.GetLatestBlobPathByPrefixAsync(prefix, ".xlsx", cancellationToken)
                ?? await _blobStorage.GetLatestBlobPathByPrefixAsync(prefix, ".csv", cancellationToken)
                ?? "";
        }
        if (string.IsNullOrWhiteSpace(resolvedBlobPath))
            throw new InvalidOperationException($"No .xlsx or .csv found under '{prefix}'.");

        var mr = maxRows <= 0 ? 100 : Math.Min(maxRows, 100);
        var sample = await _sampling.CreateSampleAsync(resolvedBlobPath, client.Id, mr, cancellationToken);
        var clientColumns = sample.Columns ?? new List<string>();

        var templates = await _templates.GetAllAsync(cancellationToken);
        var ranked = templates
            .Select(t => ScoreTemplate(t, clientColumns))
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.template.TemplateName, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        var results = new List<TemplateMatchResult>();
        foreach (var (template, score, mappingPreview) in ranked)
        {
            var confidence = Math.Round(score, 4);
            var preview = mappingPreview;

            if (useAiRefinement && template.RequiredColumns.Count > 0)
            {
                // best-effort refinement: if the endpoint isn't available yet, we still return deterministic ranking.
                try
                {
                    var refined = await _insightsEngine.RefineTemplateMappingAsync(
                        clientColumns,
                        template.RequiredColumns,
                        template.OptionalColumns,
                        cancellationToken);
                    preview = refined;
                    confidence = Math.Max(confidence, refined?.Mapping.Count > 0 ? 0.6 : confidence);
                }
                catch
                {
                    // ignore refinement failures; deterministic output remains.
                }
            }

            results.Add(new TemplateMatchResult
            {
                TemplateId = template.TemplateId,
                Name = template.TemplateName,
                Confidence = confidence,
                MappingPreview = preview
            });
        }

        return new TemplateMatchResponse
        {
            ClientCode = client.ClientCode ?? resolvedClientCode,
            BlobPath = resolvedBlobPath,
            Templates = results
        };
    }

    private static (StudioTechBI.Application.DTOs.Admin.TemplateDto template, double score, TemplateMappingPreview mappingPreview) ScoreTemplate(
        StudioTechBI.Application.DTOs.Admin.TemplateDto template,
        IReadOnlyList<string> clientColumns)
    {
        var clientSet = new HashSet<string>(clientColumns.Select(NormalizeKey), StringComparer.OrdinalIgnoreCase);

        var required = template.RequiredColumns ?? new List<string>();
        var optional = template.OptionalColumns ?? new List<string>();

        if (required.Count == 0 && optional.Count == 0)
        {
            return (template, 0, new TemplateMappingPreview { Notes = "template has no column metadata yet" });
        }

        var requiredHits = required.Count(c => clientSet.Contains(NormalizeKey(c)));
        var optionalHits = optional.Count(c => clientSet.Contains(NormalizeKey(c)));

        var requiredRatio = required.Count == 0 ? 0 : (double)requiredHits / required.Count;
        var optionalRatio = optional.Count == 0 ? 0 : (double)optionalHits / optional.Count;

        // Selection score: required overlap dominates; optional provides a boost.
        var score = 0.8 * requiredRatio + 0.2 * optionalRatio;
        score = Math.Round(score, 6);

        // Deterministic mapping preview: map template columns to exact-name client columns when possible.
        var mapping = new List<TemplateColumnMapping>();
        foreach (var col in required.Concat(optional).Distinct(StringComparer.OrdinalIgnoreCase).Take(30))
        {
            var match = clientColumns.FirstOrDefault(cc => string.Equals(NormalizeKey(cc), NormalizeKey(col), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match))
            {
                mapping.Add(new TemplateColumnMapping { TemplateColumn = col, ClientColumn = match });
            }
        }

        return (template, score, new TemplateMappingPreview { Mapping = mapping });
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

