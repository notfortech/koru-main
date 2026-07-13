using StudioTechBI.Application.DTOs.SchemaModels;

namespace StudioTechBI.Application.Interfaces;

/// <summary>Read access to the reference SchemaModel library (see SchemaModel entity for context).</summary>
public interface ISchemaModelService
{
    Task<IReadOnlyList<SchemaModelDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SchemaModelDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
