namespace StudioTechBI.Domain.Entities;

/// <summary>
/// One expected column in a <see cref="SchemaModel"/>. Drives both the deterministic
/// required/optional match scoring (via the field name lists mirrored onto Template.*ColumnsJson)
/// and the column-confirmation step, where a client sees each field alongside the data type
/// expected and the actual column from their data it was mapped to.
/// </summary>
public class SchemaModelField : BaseEntity
{
    public Guid SchemaModelId { get; set; }
    public SchemaModel SchemaModel { get; set; } = null!;

    public string FieldName { get; set; } = string.Empty;

    /// <summary>Friendly type label shown to the user, e.g. "string", "decimal", "date", "datetime", "int", "bool".</summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>Required fields dominate match scoring; missing one meaningfully lowers confidence.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Display order within the model's field list.</summary>
    public int SortOrder { get; set; }
}
