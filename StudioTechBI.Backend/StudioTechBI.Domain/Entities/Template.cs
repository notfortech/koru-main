using System;

namespace StudioTechBI.Domain.Entities;

public class Template : BaseEntity
{
    public string TemplateName { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string Version { get; set; } = string.Empty;
    public string? BlobPath { get; set; }

    /// <summary>
    /// Pre-existing column, matches dbo.Templates.ModelId (uniqueidentifier NULL) in SQL.
    /// References a `dbo.Models` table that exists in production but is NOT mapped anywhere in
    /// this codebase (found via FK_Templates_Models rejecting inserts — see MIGRATIONS.md).
    /// Do not repurpose this column or its FK for SchemaModel — use SchemaModelId instead.
    /// </summary>
    public Guid? ModelId { get; set; }

    /// <summary>
    /// References the reference schema (see <see cref="SchemaModel"/>) this dashboard template
    /// visualizes. Deliberately a separate column from the legacy ModelId (see above) — several
    /// Templates may share the same SchemaModelId (one Model, multiple dashboard layouts).
    /// </summary>
    public Guid? SchemaModelId { get; set; }
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
