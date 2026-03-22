namespace StudioTechBI.Application.DTOs.Admin;

public class DashboardDto
{
    public int TotalClients { get; set; }
    public int ActiveClients { get; set; }
    public int UploadsThisMonth { get; set; }
    public int FailedValidations { get; set; }
    public DateTime? LastDatasetRefresh { get; set; }
}
