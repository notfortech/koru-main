namespace StudioTechBI.Domain.Entities;

/// <summary>Persisted OAuth state for external connector flows (OneDrive/Google).</summary>
public class OAuthConnectionState : BaseEntity
{
    /// <summary>OneDrive, GoogleDrive, etc.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Opaque state value sent to OAuth provider.</summary>
    public string State { get; set; } = string.Empty;

    public Guid ClientId { get; set; }
    public Guid UserId { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
}

