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

    public ICollection<SchemaModelField> Fields { get; set; } = new List<SchemaModelField>();
    public ICollection<Template> Templates { get; set; } = new List<Template>();
}
