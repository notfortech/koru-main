namespace StudioTechBI.Domain.Entities;

public class Company : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ABN { get; set; }
    public string? Industry { get; set; }
    public string? Country { get; set; }
    public bool BankIntegrationEnabled { get; set; } = false;
    /// <summary>Client (folder) this company belongs to; used to resolve user's client via User → CompanyUsers → Company → Client.</summary>
    public Guid? ClientId { get; set; }

    public Client? Client { get; set; }
    public ICollection<CompanyUser> CompanyUsers { get; set; } = new List<CompanyUser>();
    public ICollection<BankConnection> BankConnections { get; set; } = new List<BankConnection>();
    public ICollection<BankTransaction> BankTransactions { get; set; } = new List<BankTransaction>();
}
