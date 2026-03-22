namespace StudioTechBI.Application.Interfaces;

public interface IAdminMaintenanceService
{
    Task RevalidateUploadAsync(string? clientId, string? filePath, CancellationToken cancellationToken = default);
    Task ReprocessJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task RefreshDatasetAsync(string? clientId, CancellationToken cancellationToken = default);
}
