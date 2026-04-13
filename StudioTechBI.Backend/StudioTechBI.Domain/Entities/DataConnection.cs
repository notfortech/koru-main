namespace StudioTechBI.Domain.Entities;

/// <summary>OAuth-backed connector configuration for a client (tokens from external OAuth flow).</summary>
public class DataConnection : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;

    /// <summary>GoogleDrive, OneDrive, SharePoint</summary>
    public string Type { get; set; } = string.Empty;

    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }

    public DateTime? ExpiresAt { get; set; }

    /// <summary>JSON: last import metadata, drive root id, etc.</summary>
    public string? ConfigJson { get; set; }
}

public static class DataConnectionTypes
{
    public const string GoogleDrive = "GoogleDrive";
    public const string OneDrive = "OneDrive";
    public const string SharePoint = "SharePoint";
}
