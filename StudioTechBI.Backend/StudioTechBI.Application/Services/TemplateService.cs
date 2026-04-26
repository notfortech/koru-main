using Microsoft.Extensions.Logging;
using System.Text.Json;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class TemplateService : BaseService, ITemplateService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
            ModelId = string.IsNullOrWhiteSpace(dto.ModelId) ? null : dto.ModelId.Trim(),
            RequiredColumnsJson = JsonSerializer.Serialize(Normalize(dto.RequiredColumns), JsonOptions),
            OptionalColumnsJson = JsonSerializer.Serialize(Normalize(dto.OptionalColumns), JsonOptions),
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
            ModelId = t.ModelId,
            RequiredColumns = DeserializeColumns(t.RequiredColumnsJson),
            OptionalColumns = DeserializeColumns(t.OptionalColumnsJson),
            CreatedDate = t.CreatedDate
        };
    }

    private static List<string> DeserializeColumns(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static List<string> Normalize(IEnumerable<string>? cols)
    {
        if (cols == null) return new List<string>();
        return cols
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
