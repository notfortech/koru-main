using System.Text.Json.Serialization;

namespace StudioTechBI.Application.DTOs.Dashboard;

/// <summary>Pre-aggregated client portal dashboard (accountant vs client). No transaction or PII fields.</summary>
public class ClientDashboardResponseDto
{
    [JsonPropertyName("kpis")]
    public DashboardKpisDto Kpis { get; set; } = new();

    [JsonPropertyName("chartData")]
    public IReadOnlyList<DashboardChartPointDto> ChartData { get; set; } = Array.Empty<DashboardChartPointDto>();

    [JsonPropertyName("quickActions")]
    public IReadOnlyList<string> QuickActions { get; set; } = Array.Empty<string>();

    /// <summary>"accountant" | "client"</summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
}

public class DashboardKpisDto
{
    [JsonPropertyName("totalBalance")]
    public decimal TotalBalance { get; set; }

    [JsonPropertyName("activeReports")]
    public int ActiveReports { get; set; }

    [JsonPropertyName("propositions")]
    public int Propositions { get; set; }

    /// <summary>Month-over-month growth percentage (revenue-based; accountant may use profit when revenue baseline is zero — see service).</summary>
    [JsonPropertyName("growthRate")]
    public decimal GrowthRate { get; set; }
}

public class DashboardChartPointDto
{
    /// <summary>ISO year-month (yyyy-MM).</summary>
    [JsonPropertyName("month")]
    public string Month { get; set; } = string.Empty;

    [JsonPropertyName("revenue")]
    public decimal Revenue { get; set; }

    [JsonPropertyName("expenses")]
    public decimal Expenses { get; set; }

    [JsonPropertyName("profit")]
    public decimal Profit { get; set; }

    /// <summary>Budget vs actual variance when available; otherwise null.</summary>
    [JsonPropertyName("variance")]
    public decimal? Variance { get; set; }
}
