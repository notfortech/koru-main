using System.Text;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.DTOs.InsightsEngine;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Application.Services;

/// <summary>
/// Maps AI/insights output to our template catalog: only real <see cref="TemplateDto"/> records are returned.
/// Scoring is deterministic (name/industry/prompt/column token overlap) until template metadata is extended.
/// </summary>
public sealed class InsightTemplateVerificationService : IInsightTemplateVerificationService
{
    private readonly ITemplateService _templateService;
    private readonly ILogger<InsightTemplateVerificationService> _logger;

    public InsightTemplateVerificationService(
        ITemplateService templateService,
        ILogger<InsightTemplateVerificationService> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    public async Task<InsightsWithTemplatesResponse> EnrichWithVerifiedTemplatesAsync(
        TransformSuggestResponse engineResponse,
        string? userPrompt,
        CancellationToken cancellationToken = default)
    {
        var templates = await _templateService.GetAllAsync(cancellationToken);
        if (templates.Count == 0)
        {
            return new InsightsWithTemplatesResponse
            {
                Insights = engineResponse,
                VerifiedTemplates = Array.Empty<VerifiedTemplateMatch>()
            };
        }

        var columnTokens = Tokenize(
            string.Join(' ', engineResponse.Columns.Select(c => c.Name ?? string.Empty).Where(n => !string.IsNullOrWhiteSpace(n))));

        var promptTokens = Tokenize(userPrompt);
        var candidateTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in columnTokens) candidateTokens.Add(t);
        foreach (var t in promptTokens) candidateTokens.Add(t);

        var matches = new List<VerifiedTemplateMatch>();
        foreach (var t in templates)
        {
            var (score, reasons) = ScoreTemplate(t, candidateTokens, userPrompt, columnTokens, promptTokens);
            if (score <= 0)
                continue;

            matches.Add(new VerifiedTemplateMatch
            {
                Template = t,
                MatchScore = Math.Round(score, 4),
                MatchReasons = reasons
            });
        }

        var ordered = matches
            .OrderByDescending(m => m.MatchScore)
            .ThenBy(m => m.Template.TemplateName, StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();

        if (ordered.Count == 0)
        {
            // No overlap: still return real catalog entries so the UI only ever shows server-known template ids.
            ordered = templates
                .OrderBy(t => t.TemplateName, StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .Select(t => new VerifiedTemplateMatch
                {
                    Template = t,
                    MatchScore = 0,
                    MatchReasons = new[] { "no_semantic_match_showing_catalog_subset" }
                })
                .ToList();
        }

        _logger.LogDebug(
            "Template verification: {TemplateCount} catalog templates, {MatchCount} scored matches.",
            templates.Count,
            ordered.Count);

        return new InsightsWithTemplatesResponse
        {
            Insights = engineResponse,
            VerifiedTemplates = ordered
        };
    }

    private static (double score, List<string> reasons) ScoreTemplate(
        TemplateDto template,
        HashSet<string> candidateTokens,
        string? userPrompt,
        IReadOnlySet<string> columnTokens,
        IReadOnlySet<string> promptTokens)
    {
        var reasons = new List<string>();
        var name = (template.TemplateName ?? string.Empty).Trim();
        var industry = (template.Industry ?? string.Empty).Trim();

        if (name.Length == 0)
            return (0, reasons);

        // Strong signal: user typed (part of) the template name
        if (!string.IsNullOrEmpty(userPrompt) && userPrompt.Contains(name, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("prompt_contains_template_name");
            return (1.0, reasons);
        }

        var nameTokens = TokenizeFromTemplateText(name);
        var industryTokens = string.IsNullOrEmpty(industry) ? Array.Empty<string>() : TokenizeFromTemplateText(industry);

        var templateTokenSet = new HashSet<string>(nameTokens, StringComparer.OrdinalIgnoreCase);
        foreach (var x in industryTokens) templateTokenSet.Add(x);

        var interCandidate = templateTokenSet.Count == 0
            ? 0
            : templateTokenSet.Count(t => candidateTokens.Contains(t));
        if (interCandidate == 0 && !string.IsNullOrEmpty(userPrompt) && !string.IsNullOrEmpty(name))
        {
            // Fuzzy: template name or industry appears as substrings in the prompt
            if (userPrompt.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("prompt_contains_name");
                return (0.95, reasons);
            }
            if (!string.IsNullOrEmpty(industry) && userPrompt.Contains(industry, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("prompt_contains_industry");
                return (0.85, reasons);
            }
        }

        if (interCandidate == 0)
        {
            // Optional: very weak link from a column that literally matches a name token
            if (nameTokens.Length > 0)
            {
                var hit = nameTokens.Count(nt => columnTokens.Contains(nt));
                if (hit > 0)
                {
                    var s = 0.25 * hit / (double)nameTokens.Length;
                    reasons.Add("column_name_keyword_overlap");
                    return (Math.Min(0.4, s), reasons);
                }
            }

            return (0, reasons);
        }

        var union = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var x in templateTokenSet) union.Add(x);
        foreach (var x in candidateTokens) union.Add(x);

        var jacc = union.Count == 0 ? 0 : (double)interCandidate / union.Count;
        if (jacc > 0) reasons.Add("token_overlap");

        if (!string.IsNullOrEmpty(industry) && columnTokens.Count > 0 && industryTokens.Length > 0)
        {
            if (industryTokens.Any(it => columnTokens.Contains(it) || userPrompt?.Contains(it, StringComparison.OrdinalIgnoreCase) == true))
            {
                reasons.Add("industry_context");
                jacc = Math.Max(jacc, 0.35);
            }
        }

        if (jacc < 0.08)
            return (0, reasons);

        jacc = Math.Min(0.9, jacc);
        return (jacc, reasons);
    }

    private static IReadOnlySet<string> Tokenize(string? text)
    {
        var s = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in TokenizeFromTemplateText((text ?? string.Empty).Trim()))
            s.Add(w);
        return s;
    }

    private static string[] TokenizeFromTemplateText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();

        var sb = new StringBuilder(text.Length);
        foreach (var c in text.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');

        return sb.ToString()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => x.Length >= 2)
            .Distinct()
            .ToArray();
    }
}
