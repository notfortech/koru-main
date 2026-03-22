namespace StudioTechBI.Application.DTOs.Auth;

/// <summary>Result of resolving client via User → CompanyUsers → Company → Client (when user is active).</summary>
public class ClientInfoFromCompanyDto
{
    public Guid ClientId { get; set; }
    public string ClientCode { get; set; } = string.Empty;
    public string? ClientName { get; set; }
}
