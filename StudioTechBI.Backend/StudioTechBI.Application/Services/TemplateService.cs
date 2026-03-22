using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class TemplateService : BaseService, ITemplateService
{
    private readonly IRepository<Template> _templateRepository;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<TemplateService> _logger;

    public TemplateService(
        IUnitOfWork unitOfWork,
        IRepository<Template> templateRepository,
        IBlobStorageService blobStorage,
        ILogger<TemplateService> logger)
        : base(unitOfWork)
    {
        _templateRepository = templateRepository;
        _blobStorage = blobStorage;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TemplateDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = await _templateRepository.GetAllAsync(cancellationToken);
        return list.Where(t => !t.IsDeleted).Select(MapToDto).ToList();
    }

    public async Task<TemplateDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var t = await _templateRepository.GetByIdAsync(id, cancellationToken);
        return t == null || t.IsDeleted ? null : MapToDto(t);
    }

    public async Task<TemplateDto> CreateAsync(TemplateCreateDto dto, Stream? fileContent = null, string? fileName = null, CancellationToken cancellationToken = default)
    {
        var template = new Template
        {
            Id = Guid.NewGuid(),
            TemplateName = dto.TemplateName.Trim(),
            Industry = dto.Industry?.Trim(),
            Version = dto.Version.Trim(),
            CreatedDate = DateTime.UtcNow
        };

        if (fileContent != null && !string.IsNullOrEmpty(fileName))
        {
            template.BlobPath = await _blobStorage.UploadTemplateAsync(
                dto.TemplateName,
                dto.Industry ?? "default",
                dto.Version,
                fileContent,
                fileName,
                cancellationToken);
        }

        await _templateRepository.AddAsync(template, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created template {TemplateId}", template.Id);
        return MapToDto(template);
    }

    private static TemplateDto MapToDto(Template t)
    {
        return new TemplateDto
        {
            TemplateId = t.Id,
            TemplateName = t.TemplateName,
            Industry = t.Industry,
            Version = t.Version,
            BlobPath = t.BlobPath,
            CreatedDate = t.CreatedDate
        };
    }
}
