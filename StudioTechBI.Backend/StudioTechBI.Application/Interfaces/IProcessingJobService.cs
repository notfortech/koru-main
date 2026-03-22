using StudioTechBI.Application.DTOs.Admin;

namespace StudioTechBI.Application.Interfaces;

public interface IProcessingJobService
{
    Task<IReadOnlyList<ProcessingJobDto>> GetAllAsync(int? limit, CancellationToken cancellationToken = default);
    Task<ProcessingJobDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
