using System;

namespace StudioTechBI.Domain.Entities;

public class Template : BaseEntity
{
    public string TemplateName { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string Version { get; set; } = string.Empty;
    public string? BlobPath { get; set; }

    /// <summary>
    /// External semantic model identifier (matches dbo.Templates.ModelId, uniqueidentifier in SQL).
    /// </summary>
    public Guid? ModelId { get; set; }

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
