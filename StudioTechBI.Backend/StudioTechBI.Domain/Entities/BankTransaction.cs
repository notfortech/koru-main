namespace StudioTechBI.Domain.Entities;

public class BankTransaction : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Guid? BankConnectionId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }

    public Company? Company { get; set; }
    public BankConnection? BankConnection { get; set; }
}
