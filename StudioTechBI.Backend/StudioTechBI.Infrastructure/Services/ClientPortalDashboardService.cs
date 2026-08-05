using Microsoft.EntityFrameworkCore;
using StudioTechBI.Application.DTOs.Dashboard;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Enums;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.Services;

/// <summary>
/// Aggregates dashboard KPIs and chart series from EF entities only (no line items, account numbers, or tax IDs).
/// Financial series use bank transaction amounts: revenue = sum(positive), expenses = sum(|negative|), profit = revenue - expenses.
/// </summary>
public class ClientPortalDashboardService : IClientPortalDashboardService
{
    /// <summary>FunctionalLogs.EventType values for proposal KPIs (insert via your domain when proposals exist).</summary>
    public const string ProposalSentEventType = "Proposal.Sent";

    public const string ProposalPendingActionEventType = "Proposal.PendingAction";

    private readonly ApplicationDbContext _db;
    private readonly ILocalCreditLedgerService _localCredits;

    public ClientPortalDashboardService(ApplicationDbContext db, ILocalCreditLedgerService localCredits)
    {
        _db = db;
        _localCredits = localCredits;
    }

    public async Task<ClientDashboardResponseDto> GetDashboardAsync(
        DashboardRequestContext context,
        int months,
        CancellationToken cancellationToken = default)
    {
        months = Math.Clamp(months, 1, 12);
        var rangeEnd = DateTime.UtcNow.Date.AddDays(1);
        var chartStart = StartOfUtcMonth(DateTime.UtcNow).AddMonths(-(months - 1));

        if (string.IsNullOrWhiteSpace(context.Email) || context.ScopedClientIds.Count == 0)
        {
            return EmptyResponse(context);
        }

        var clientIds = context.ScopedClientIds.Distinct().ToList();
        var companyIds = await _db.Companies.AsNoTracking()
            .Where(c => !c.IsDeleted && c.ClientId != null && clientIds.Contains(c.ClientId!.Value))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        var monthlyAgg = companyIds.Count == 0
            ? new Dictionary<(int Y, int M), (decimal Rev, decimal Exp)>()
            : await GetMonthlyAggregatesAsync(companyIds, chartStart, rangeEnd, cancellationToken);

        var chartData = BuildChartSeries(monthlyAgg, chartStart, months);

        var (lastRev, prevRev, lastProfit, prevProfit) = GetLastTwoMonthMetrics(chartData);

        decimal totalBalance;
        if (context.IsClientView)
        {
            // Single-client: net movement in window as "cash summary" (no account-level detail).
            totalBalance = RoundMoney(monthlyAgg.Values.Sum(v => v.Rev - v.Exp));
        }
        else
        {
            // Accountant: net position proxy = assets (inflows) minus liabilities (outflows as positive sums).
            var windowRev = monthlyAgg.Values.Sum(v => v.Rev);
            var windowExp = monthlyAgg.Values.Sum(v => v.Exp);
            totalBalance = RoundMoney(windowRev - windowExp);
        }

        var activeReports = await CountActiveReportsAsync(context, clientIds, chartStart, rangeEnd, cancellationToken);
        var propositions = await CountPropositionsAsync(context, clientIds, cancellationToken);

        var growthRate = context.IsClientView || lastRev > 0
            ? ComputeGrowthPct(lastRev, prevRev)
            : ComputeGrowthPct(lastProfit, prevProfit);

        // Single-client only -- "reports generated"/"AI credits" aren't accountant-firm-wide
        // concepts, so both stay null and the chart stays empty outside client view.
        int? reportsGenerated = null;
        int? aiCreditsRemaining = null;
        var reportGenerationChartData = (IReadOnlyList<ReportGenerationChartPointDto>)Array.Empty<ReportGenerationChartPointDto>();

        if (context.IsClientView && clientIds.Count == 1)
        {
            var singleClientId = clientIds[0];

            reportsGenerated = await _db.ReportGenerationEvents.AsNoTracking()
                .CountAsync(e => !e.IsDeleted && e.ClientId == singleClientId, cancellationToken);

            aiCreditsRemaining = await _localCredits.GetBalanceAsync(singleClientId, cancellationToken);

            var reportGenRows = await _db.ReportGenerationEvents.AsNoTracking()
                .Where(e => !e.IsDeleted
                            && e.ClientId == singleClientId
                            && e.CreatedAt >= chartStart
                            && e.CreatedAt < rangeEnd)
                .GroupBy(e => new { e.CreatedAt.Year, e.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var reportGenCounts = reportGenRows.ToDictionary(r => (r.Year, r.Month), r => r.Count);
            reportGenerationChartData = BuildReportGenerationChartSeries(reportGenCounts, chartStart, months);
        }

        return new ClientDashboardResponseDto
        {
            Role = context.IsAccountantView ? "accountant" : "client",
            Kpis = new DashboardKpisDto
            {
                TotalBalance = totalBalance,
                ActiveReports = activeReports,
                Propositions = propositions,
                GrowthRate = Math.Round(growthRate, 1, MidpointRounding.AwayFromZero),
                ReportsGenerated = reportsGenerated,
                AiCreditsRemaining = aiCreditsRemaining
            },
            ChartData = chartData,
            ReportGenerationChartData = reportGenerationChartData,
            QuickActions = BuildQuickActions(context)
        };
    }

    private static IReadOnlyList<ReportGenerationChartPointDto> BuildReportGenerationChartSeries(
        Dictionary<(int Y, int M), int> counts,
        DateTime firstMonthStart,
        int monthCount)
    {
        var list = new List<ReportGenerationChartPointDto>(monthCount);
        var cursor = firstMonthStart;
        for (var i = 0; i < monthCount; i++)
        {
            counts.TryGetValue((cursor.Year, cursor.Month), out var count);
            list.Add(new ReportGenerationChartPointDto
            {
                Month = $"{cursor.Year:D4}-{cursor.Month:D2}",
                Count = count
            });
            cursor = cursor.AddMonths(1);
        }

        return list;
    }

    private static ClientDashboardResponseDto EmptyResponse(DashboardRequestContext context)
    {
        return new ClientDashboardResponseDto
        {
            Role = context.IsAccountantView ? "accountant" : "client",
            Kpis = new DashboardKpisDto(),
            ChartData = Array.Empty<DashboardChartPointDto>(),
            QuickActions = BuildQuickActions(context)
        };
    }

    // "View Report Stats" is the only clickable one (see PortalDashboardPanel.tsx's
    // handleQuickAction) -- everything else stays informational text, matching the original
    // plain-string list this replaced.
    private static IReadOnlyList<QuickActionDto> BuildQuickActions(DashboardRequestContext context)
    {
        return context.IsAccountantView
            ? new[]
            {
                new QuickActionDto("Upload Report"),
                new QuickActionDto("Create Proposal"),
                new QuickActionDto("Request Documents"),
                new QuickActionDto("Flag Issue"),
            }
            : new[]
            {
                new QuickActionDto("View Reports"),
                new QuickActionDto("Upload Documents"),
                new QuickActionDto("Accept Proposal"),
                new QuickActionDto("Contact Accountant"),
                new QuickActionDto("View Report Stats", "view-report-stats"),
            };
    }

    private async Task<Dictionary<(int Y, int M), (decimal Rev, decimal Exp)>> GetMonthlyAggregatesAsync(
        List<Guid> companyIds,
        DateTime rangeStart,
        DateTime rangeEndExclusive,
        CancellationToken cancellationToken)
    {
        var rows = await _db.BankTransactions.AsNoTracking()
            .Where(t => !t.IsDeleted
                        && companyIds.Contains(t.CompanyId)
                        && t.TransactionDate >= rangeStart
                        && t.TransactionDate < rangeEndExclusive)
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Revenue = g.Sum(x => x.Amount > 0 ? x.Amount : 0m),
                Expenses = g.Sum(x => x.Amount < 0 ? -x.Amount : 0m)
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            r => (r.Year, r.Month),
            r => (r.Revenue, r.Expenses));
    }

    private static IReadOnlyList<DashboardChartPointDto> BuildChartSeries(
        Dictionary<(int Y, int M), (decimal Rev, decimal Exp)> agg,
        DateTime firstMonthStart,
        int monthCount)
    {
        var list = new List<DashboardChartPointDto>(monthCount);
        var cursor = firstMonthStart;
        for (var i = 0; i < monthCount; i++)
        {
            var key = (cursor.Year, cursor.Month);
            agg.TryGetValue(key, out var pair);
            var revenue = RoundMoney(pair.Rev);
            var expenses = RoundMoney(pair.Exp);
            var profit = RoundMoney(revenue - expenses);
            list.Add(new DashboardChartPointDto
            {
                Month = $"{cursor.Year:D4}-{cursor.Month:D2}",
                Revenue = revenue,
                Expenses = expenses,
                Profit = profit,
                Variance = null
            });
            cursor = cursor.AddMonths(1);
        }

        return list;
    }

    private static (decimal lastRev, decimal prevRev, decimal lastProfit, decimal prevProfit) GetLastTwoMonthMetrics(
        IReadOnlyList<DashboardChartPointDto> chart)
    {
        if (chart.Count == 0)
            return (0, 0, 0, 0);
        if (chart.Count == 1)
            return (chart[0].Revenue, 0, chart[0].Profit, 0);
        var last = chart[^1];
        var prev = chart[^2];
        return (last.Revenue, prev.Revenue, last.Profit, prev.Profit);
    }

    private async Task<int> CountActiveReportsAsync(
        DashboardRequestContext context,
        List<Guid> clientIds,
        DateTime rangeStart,
        DateTime rangeEndExclusive,
        CancellationToken cancellationToken)
    {
        if (context.IsAccountantView)
        {
            return await _db.ProcessingJobs.AsNoTracking()
                .Where(j => !j.IsDeleted
                            && clientIds.Contains(j.ClientId)
                            && j.Status == JobStatus.Completed
                            && j.CreatedDate >= rangeStart
                            && j.CreatedDate < rangeEndExclusive)
                .CountAsync(cancellationToken);
        }

        return await _db.PowerBiAssets.AsNoTracking()
            .Where(a => a.IsActive == true && a.ClientId != null && clientIds.Contains(a.ClientId!.Value))
            .CountAsync(cancellationToken);
    }

    private async Task<int> CountPropositionsAsync(
        DashboardRequestContext context,
        List<Guid> clientIds,
        CancellationToken cancellationToken)
    {
        if (context.IsAccountantView)
        {
            return await _db.FunctionalLogs.AsNoTracking()
                .Where(l => l.EventType == ProposalSentEventType
                            && l.ClientId != null
                            && clientIds.Contains(l.ClientId!.Value))
                .CountAsync(cancellationToken);
        }

        return await _db.FunctionalLogs.AsNoTracking()
            .Where(l => l.EventType == ProposalPendingActionEventType
                        && l.ClientId != null
                        && clientIds.Contains(l.ClientId!.Value))
            .CountAsync(cancellationToken);
    }

    private static decimal ComputeGrowthPct(decimal current, decimal previous)
    {
        if (previous <= 0m)
            return current > 0m ? 100m : 0m;
        return (current - previous) / previous * 100m;
    }

    private static DateTime StartOfUtcMonth(DateTime utc) =>
        new(utc.Year, utc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    private static decimal RoundMoney(decimal v) =>
        Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
