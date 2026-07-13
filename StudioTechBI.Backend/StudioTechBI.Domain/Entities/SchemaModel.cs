namespace StudioTechBI.Domain.Entities;

/// <summary>
/// A reference schema for one industry data shape (e.g. "NDIS — Participant Service
/// Delivery"). Used to score a connecting client's column headers against a known
/// shape (see <see cref="SchemaModelField"/>), and groups the one or more Dashboard
/// Templates (<see cref="Template.ModelId"/>) that visualize it.
///
/// This is the "Model" in the schema/model/template matching pipeline — distinct from
/// <see cref="InsightModel"/>, which is an unrelated, per-client AI-drafted model used
/// by the Insights Engine feature.
/// </summary>
public class SchemaModel : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>True when this model was proposed by the AI matching pipeline rather than admin-curated/seeded.</summary>
    public bool IsAiSuggested { get; set; }

    /// <summary>
    /// "Approved" | "PendingReview" | "Rejected". Only Approved models are used for matching
    /// other clients' schemas — an AI-suggested model must not influence other clients until
    /// support has reviewed it. Seeded/admin-curated models default to Approved.
    /// </summary>
    public string ReviewStatus { get; set; } = "Approved";

    /// <summary>Traceability only — which client's match session caused the AI to propose this model, if any.</summary>
    public Guid? SuggestedByClientId { get; set; }

    public ICollection<SchemaModelField> Fields { get; set; } = new List<SchemaModelField>();
    public ICollection<Template> Templates { get; set; } = new List<Template>();
}
