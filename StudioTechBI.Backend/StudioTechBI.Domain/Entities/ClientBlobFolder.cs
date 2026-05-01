namespace StudioTechBI.Domain.Entities;

/// <summary>Logical blob path hint for admins (container name + folder key), e.g. &quot;clients/AU-001&quot;. Physical keys remain &quot;AU-001/accounting/...&quot; in the Azure &quot;clients&quot; container.</summary>
public class ClientBlobFolder : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }
    /// <summary>e.g. clients/AU-001</summary>
    public string FolderPath { get; set; } = string.Empty;
}
