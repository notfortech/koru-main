using StudioTechBI.Application.DTOs.Admin;

namespace StudioTechBI.Application.Interfaces;

public interface IAdminLoggingService
{
    Task<IReadOnlyList<FunctionalLogDto>> GetFunctionalLogsAsync(int? limit, Guid? clientId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TechnicalLogDto>> GetTechnicalLogsAsync(int? limit, string? level, CancellationToken cancellationToken = default);
    Task LogAdminActionAsync(string service, string message, CancellationToken cancellationToken = default);
}
