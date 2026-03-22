namespace StudioTechBI.Domain.Entities;

public class CompanyUser : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public int Role { get; set; }

    public Company? Company { get; set; }
    public User? User { get; set; }
}
