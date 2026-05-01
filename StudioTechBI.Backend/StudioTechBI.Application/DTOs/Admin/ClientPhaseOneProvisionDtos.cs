using System.ComponentModel.DataAnnotations;

namespace StudioTechBI.Application.DTOs.Admin;

/// <summary>Automates Phase 1 onboarding: create client tenant + link an existing portal user (no Power BI asset ids).</summary>
public sealed class ClientPhaseOneProvisionDto
{
    /// <summary>Folder key for blob paths (e.g. AU-004). Must be unique; must not already exist.</summary>
    [Required]
    [StringLength(32)]
    public string ClientCode { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string ClientName { get; set; } = string.Empty;

    public string? Industry { get; set; }
    public string? TemplateVersion { get; set; }

    /// <summary>Link this user to the new client. Provide this or <see cref="UserEmail"/>.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Alternative to UserId: resolve user by email (normalized to lowercase).</summary>
    [StringLength(256)]
    [EmailAddress]
    public string? UserEmail { get; set; }

    /// <summary>If omitted, defaults to &quot;{ClientName} Company&quot;.</summary>
    [StringLength(256)]
    public string? CompanyName { get; set; }
}

public sealed class ClientPhaseOneProvisionResultDto
{
    public ClientDto Client { get; set; } = null!;
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    /// <summary>Logical blob path hint, e.g. clients/AU-001 (physical keys stay AU-001/...).</summary>
    public string BlobFolderPath { get; set; } = string.Empty;
}
