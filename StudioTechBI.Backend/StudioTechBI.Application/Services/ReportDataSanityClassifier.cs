using StudioTechBI.Application.DTOs.ReportGenerator;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Application.Services;

/// <summary>
/// Classifies a GeneratedReportDto's data-sanity health into ReportValidationCheck rows. Reasons
/// entirely over the exact snapshot the user submitted (dto.Warnings + dto.Kpis/Charts) — never
/// re-invokes the Python engine, so validation can't diverge from what's actually on screen.
/// </summary>
public static class ReportDataSanityClassifier
{
    private const string DataSanityPrefix = "[data-sanity]";

    public static List<ReportValidationCheck> Classify(GeneratedReportDto dto)
    {
        var checks = new List<ReportValidationCheck>();
        var sortOrder = 0;

        // 1. Structured [data-sanity] warnings from the Python engine (generic_report_engine.py) —
        // Fail for value-destroying issues (non-finite KPI dropped), Warning for everything else
        // (all-zero series, single-category breakdown, implausible magnitude, mostly-null column).
        var sanityWarnings = (dto.Warnings ?? new List<string>())
            .Where(w => w.StartsWith(DataSanityPrefix, StringComparison.Ordinal))
            .ToList();

        foreach (var warning in sanityWarnings)
        {
            var status = warning.Contains("non-finite", StringComparison.OrdinalIgnoreCase)
                ? ReportValidationCheckStatuses.Fail
                : ReportValidationCheckStatuses.Warning;

            checks.Add(new ReportValidationCheck
            {
                Id = Guid.NewGuid(),
                CheckFamily = ReportValidationCheckFamilies.DataSanity,
                CheckName = "engine-warning",
                Status = status,
                Detail = warning[DataSanityPrefix.Length..].Trim(),
                SortOrder = sortOrder++
            });
        }

        // 2. Defensive direct guard: catch a NaN/Infinity KPI that reached the DTO despite the
        // Python-side guard (belt-and-suspenders, not the primary detection mechanism).
        foreach (var kpi in dto.Kpis ?? new List<ReportKpiDto>())
        {
            if (double.IsNaN(kpi.Value) || double.IsInfinity(kpi.Value))
            {
                checks.Add(new ReportValidationCheck
                {
                    Id = Guid.NewGuid(),
                    CheckFamily = ReportValidationCheckFamilies.DataSanity,
                    CheckName = "kpi-finite-values",
                    Status = ReportValidationCheckStatuses.Fail,
                    Detail = $"KPI \"{kpi.Label}\" computed as {kpi.Value} — not a finite value.",
                    SortOrder = sortOrder++
                });
            }
        }

        // 3. No data produced at all.
        if ((dto.Kpis?.Count ?? 0) == 0 && (dto.Charts?.Count ?? 0) == 0)
        {
            checks.Add(new ReportValidationCheck
            {
                Id = Guid.NewGuid(),
                CheckFamily = ReportValidationCheckFamilies.DataSanity,
                CheckName = "report-has-data",
                Status = ReportValidationCheckStatuses.Fail,
                Detail = "This report produced no KPIs and no charts.",
                SortOrder = sortOrder++
            });
        }

        // 4. Nothing flagged — a clean Pass row so the run always has at least one Data Sanity result.
        if (checks.Count == 0)
        {
            checks.Add(new ReportValidationCheck
            {
                Id = Guid.NewGuid(),
                CheckFamily = ReportValidationCheckFamilies.DataSanity,
                CheckName = "no-issues-found",
                Status = ReportValidationCheckStatuses.Pass,
                Detail = "No data sanity issues found.",
                SortOrder = sortOrder++
            });
        }

        return checks;
    }
}
