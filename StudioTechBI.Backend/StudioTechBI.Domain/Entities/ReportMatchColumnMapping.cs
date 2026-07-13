namespace StudioTechBI.Domain.Entities;

/// <summary>
/// One SchemaModelField-to-client-column mapping within a <see cref="ReportMatchDraft"/>.
/// Included defaults true when a client column was found for the field; the user can
/// deselect it in the column-confirmation step, and deselected fields must never reach
/// the deterministic data-push pipeline.
/// </summary>
public class ReportMatchColumnMapping : BaseEntity
{
    public Guid ReportMatchDraftId { get; set; }
    public ReportMatchDraft ReportMatchDraft { get; set; } = null!;

    /// <summary>The SchemaModelField name this row maps.</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>Expected data type, carried through for the confirmation UI.</summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>The client's actual column that was matched — null if no confident match was found for this field.</summary>
    public string? ClientColumnName { get; set; }

    public bool Included { get; set; } = true;
}
