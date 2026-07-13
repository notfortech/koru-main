using System;

namespace StudioTechBI.Domain.Entities;

public class Template : BaseEntity
{
    public string TemplateName { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string Version { get; set; } = string.Empty;
    public string? BlobPath { get; set; }

    /// <summary>
    /// References the reference schema (see <see cref="SchemaModel"/>) this dashboard template
    /// visualizes. Matches dbo.Templates.ModelId, uniqueidentifier NULL in SQL — several
    /// Templates may share the same ModelId (one Model, multiple dashboard layouts).
    /// </summary>
    public Guid? ModelId { get; set; }
    public SchemaModel? Model { get; set; }

    /// <summary>
    /// JSON array of required column names used to match client schemas.
    /// </summary>
    public string RequiredColumnsJson { get; set; } = "[]";

    /// <summary>
    /// JSON array of optional column names used to boost match score.
    /// </summary>
    public string OptionalColumnsJson { get; set; } = "[]";
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
