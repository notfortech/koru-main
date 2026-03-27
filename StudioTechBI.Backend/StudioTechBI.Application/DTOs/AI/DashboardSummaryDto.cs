namespace StudioTechBI.Application.DTOs.AI;

public class DashboardSummaryDto
{
    public string Period { get; set; }
    public double Revenue { get; set; }
    public double Expenses { get; set; }
    public double Profit { get; set; }
    public double GrowthPercentage { get; set; }
    public string TopRegion { get; set; }
}