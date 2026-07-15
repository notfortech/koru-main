using StudioTechBI.Application.DTOs.SchemaModels;

namespace StudioTechBI.Application.Interfaces;

/// <summary>Admin review queue for learned column aliases — see SchemaModelFieldAlias entity.</summary>
public interface ISchemaModelFieldAliasService
{
    /// <summary>Learned aliases awaiting review (ApprovalStatus = PendingReview).</summary>
    Task<IReadOnlyList<SchemaModelFieldAliasDto>> GetPendingReviewAsync(CancellationToken cancellationToken = default);

    /// <summary>Approves a learned alias, making it eligible for other clients' matching.</summary>
    Task<SchemaModelFieldAliasDto?> ApproveAsync(Guid id, string approvedBy, CancellationToken cancellationToken = default);

    /// <summary>Rejects a learned alias; it stays out of matching permanently.</summary>
    Task<SchemaModelFieldAliasDto?> RejectAsync(Guid id, string rejectedBy, CancellationToken cancellationToken = default);
}
