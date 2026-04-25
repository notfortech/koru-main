namespace StudioTechBI.Application.DTOs.InsightsEngine.Canonical;

public sealed class DashboardDefinition
{
    public List<DashboardPage> Pages { get; set; } = new();
}

public sealed class DashboardPage
{
    public string Name { get; set; } = string.Empty;
    public List<DashboardVisual> Visuals { get; set; } = new();
}

public sealed class DashboardVisual
{
    public string Type { get; set; } = string.Empty; // kpi, line_chart, bar_chart, table
    public string Title { get; set; } = string.Empty;

    public string? Measure { get; set; } // KPI
    public string? X { get; set; }
    public string? Y { get; set; }
    public string? Category { get; set; }
    public string? Value { get; set; }
}

