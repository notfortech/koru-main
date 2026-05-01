using StudioTechBI.Application.DTOs.Admin;

namespace StudioTechBI.Application.Interfaces;

public interface IClientService
{
    Task<ClientDto> CreateAsync(ClientCreateDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ClientDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ClientDto?> GetByClientCodeAsync(string clientCode, CancellationToken cancellationToken = default);
    /// <summary>Get client by folder code (e.g. AU-001) or by client Id (GUID). Use for embed-token when frontend may send either.</summary>
    Task<ClientDto?> GetByClientCodeOrIdAsync(string clientCodeOrId, CancellationToken cancellationToken = default);
    Task<ClientDto?> UpdateAsync(Guid id, ClientUpdateDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Assigns a user to a client so they can access that client's reports. Admin only.</summary>
    Task<bool> AssignUserToClientAsync(Guid userId, Guid clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 1 provisioning: create <see cref="Client"/>, link user, set <see cref="User.IsActive"/>,
    /// ensure &quot;Client&quot; <see cref="UserRole"/>. Does not create Power BI assets (Phase 2).
    /// </summary>
    Task<ClientPhaseOneProvisionResultDto> ProvisionPhaseOneAsync(
        ClientPhaseOneProvisionDto dto,
        CancellationToken cancellationToken = default);
    /// <summary>Returns the client code (e.g. AU-001) for the given user email if the user is linked to a client. Used to add client_code claim when token lacks it.</summary>
    Task<string?> GetClientCodeForUserEmailAsync(string email, CancellationToken cancellationToken = default);
}
