namespace StudioTechBI.Domain.Entities;

public class BankConnection : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTime? LastSyncDate { get; set; }

    public Company? Company { get; set; }
    public ICollection<BankTransaction> BankTransactions { get; set; } = new List<BankTransaction>();
}
