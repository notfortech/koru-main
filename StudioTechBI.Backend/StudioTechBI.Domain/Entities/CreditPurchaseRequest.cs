namespace StudioTechBI.Domain.Entities;

/// <summary>
/// A client's request to buy additional AI credits — mock checkout for now (no real payment
/// gateway), fulfilled manually by an admin once payment is confirmed out of band. Mirrors
/// CustomReportRequest's Pending/Fulfilled shape: the client files a request, an admin marks it
/// resolved, and that status transition is the only "flag" driving whether the credits have
/// landed — once Status becomes Paid, the credits are already granted against AgentHost's ledger
/// (see AdminCreditPurchaseRequestsController.MarkPaidAsync), so the client's next credit-balance
/// fetch reflects the new total with no separate propagation step.
/// </summary>
public class CreditPurchaseRequest : BaseEntity
{
    public Guid ClientId { get; set; }
    public string? RequestedByEmail { get; set; }
    public int CreditsRequested { get; set; }
    public string PackLabel { get; set; } = string.Empty;
    public string Status { get; set; } = CreditPurchaseRequestStatuses.Pending;
    public string? Notes { get; set; }

    public DateTime? PaidAtUtc { get; set; }
    public string? PaidByEmail { get; set; }
}
