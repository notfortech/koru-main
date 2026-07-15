using StudioTechBI.Application.DTOs.SchemaModels;

namespace StudioTechBI.Application.Interfaces;

/// <summary>Read access to the reference SchemaModel library (see SchemaModel entity for context).</summary>
public interface ISchemaModelService
{
    Task<IReadOnlyList<SchemaModelDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SchemaModelDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>AI-proposed models awaiting support review (ReviewStatus = PendingReview).</summary>
    Task<IReadOnlyList<SchemaModelDto>> GetPendingReviewAsync(CancellationToken cancellationToken = default);

    /// <summary>Approves an AI-proposed model, making it eligible for other clients' matching.</summary>
    Task<SchemaModelDto?> ApproveAsync(Guid id, string approvedBy, CancellationToken cancellationToken = default);

    /// <summary>Rejects an AI-proposed model; it stays out of matching permanently.</summary>
    Task<SchemaModelDto?> RejectAsync(Guid id, string rejectedBy, CancellationToken cancellationToken = default);
}
