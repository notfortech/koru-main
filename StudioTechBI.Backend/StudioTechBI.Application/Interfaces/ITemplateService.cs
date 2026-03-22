using StudioTechBI.Application.DTOs.Admin;

namespace StudioTechBI.Application.Interfaces;

public interface ITemplateService
{
    Task<IReadOnlyList<TemplateDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TemplateDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TemplateDto> CreateAsync(TemplateCreateDto dto, Stream? fileContent = null, string? fileName = null, CancellationToken cancellationToken = default);
}
