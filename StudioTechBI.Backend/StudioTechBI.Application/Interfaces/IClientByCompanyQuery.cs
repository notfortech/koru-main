using StudioTechBI.Application.DTOs.Auth;

namespace StudioTechBI.Application.Interfaces;

/// <summary>Resolves client (ClientId, ClientCode) for a user by email via User → CompanyUsers → Company → Client. Used when user is active.</summary>
public interface IClientByCompanyQuery
{
    /// <summary>Returns client info for the given user email using the company path join. Returns null if no row found.</summary>
    Task<ClientInfoFromCompanyDto?> GetClientForUserEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Returns all clients the user can access via User → CompanyUsers → Company → Client (e.g. for accounting firm with multiple clients). Distinct by ClientId.</summary>
    Task<IReadOnlyList<ClientInfoFromCompanyDto>> GetClientsForUserEmailAsync(string email, CancellationToken cancellationToken = default);
}
