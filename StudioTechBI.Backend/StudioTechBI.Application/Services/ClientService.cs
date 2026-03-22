using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class ClientService : BaseService, IClientService
{
    private readonly IRepository<Client> _clientRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<ClientService> _logger;
    private readonly IClientByCompanyQuery _clientByCompanyQuery;

    public ClientService(
        IUnitOfWork unitOfWork,
        IRepository<Client> clientRepository,
        IRepository<User> userRepository,
        IBlobStorageService blobStorage,
        ILogger<ClientService> logger,
        IClientByCompanyQuery clientByCompanyQuery)
        : base(unitOfWork)
    {
        _clientRepository = clientRepository;
        _userRepository = userRepository;
        _blobStorage = blobStorage;
        _logger = logger;
        _clientByCompanyQuery = clientByCompanyQuery;
    }

    public async Task<ClientDto> CreateAsync(ClientCreateDto dto, CancellationToken cancellationToken = default)
    {
        var code = (dto.ClientCode ?? "").Trim();
        if (string.IsNullOrEmpty(code))
            throw new InvalidOperationException("ClientCode is required (e.g. AU-001).");
        var client = new Client
        {
            Id = Guid.NewGuid(),
            ClientCode = code,
            ClientName = dto.ClientName.Trim(),
            Industry = dto.Industry?.Trim(),
            TemplateVersion = dto.TemplateVersion?.Trim(),
            IsActive = true,
            BlobFolderPath = code,
            CreatedDate = DateTime.UtcNow,
            PowerBIWorkspaceId = dto.PowerBIWorkspaceId?.Trim(),
            PowerBIDatasetId = dto.PowerBIDatasetId?.Trim(),
            PowerBIReportId = dto.PowerBIReportId?.Trim()
        };
        await _clientRepository.AddAsync(client, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await _blobStorage.CreateClientFolderStructureAsync(client.ClientCode ?? client.Id.ToString(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blob folder creation failed for client {ClientId}", client.Id);
        }

        _logger.LogInformation("Created client {ClientId}", client.Id);
        return MapToDto(client);
    }

    public async Task<IReadOnlyList<ClientDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = await _clientRepository.GetAllAsync(cancellationToken);
        return list.Where(c => !c.IsDeleted).Select(MapToDto).ToList();
    }

    public async Task<ClientDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var client = await _clientRepository.GetByIdAsync(id, cancellationToken);
        return client == null || client.IsDeleted ? null : MapToDto(client);
    }

    public async Task<ClientDto?> GetByClientCodeAsync(string clientCode, CancellationToken cancellationToken = default)
    {
        var code = (clientCode ?? "").Trim();
        if (string.IsNullOrEmpty(code)) return null;
        var codeLower = code.ToLowerInvariant();
        var clients = await _clientRepository.FindAsync(c => !c.IsDeleted && c.ClientCode != null && c.ClientCode.ToLower() == codeLower, cancellationToken);
        var client = clients.FirstOrDefault();
        return client == null ? null : MapToDto(client);
    }

    public async Task<ClientDto?> GetByClientCodeOrIdAsync(string clientCodeOrId, CancellationToken cancellationToken = default)
    {
        var s = (clientCodeOrId ?? "").Trim();
        if (string.IsNullOrEmpty(s)) return null;
        if (Guid.TryParse(s, out var id))
        {
            var byId = await GetByIdAsync(id, cancellationToken);
            if (byId != null) return byId;
        }
        return await GetByClientCodeAsync(s, cancellationToken);
    }

    public async Task<ClientDto?> UpdateAsync(Guid id, ClientUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var client = await _clientRepository.GetByIdAsync(id, cancellationToken);
        if (client == null || client.IsDeleted) return null;

        if (dto.ClientCode != null)
            client.ClientCode = dto.ClientCode.Trim();
        client.ClientName = dto.ClientName.Trim();
        client.Industry = dto.Industry?.Trim();
        client.TemplateVersion = dto.TemplateVersion?.Trim();
        client.IsActive = dto.IsActive;
        if (!string.IsNullOrEmpty(client.ClientCode))
            client.BlobFolderPath = client.ClientCode;
        if (dto.PowerBIWorkspaceId != null) client.PowerBIWorkspaceId = dto.PowerBIWorkspaceId.Trim();
        if (dto.PowerBIDatasetId != null) client.PowerBIDatasetId = dto.PowerBIDatasetId.Trim();
        if (dto.PowerBIReportId != null) client.PowerBIReportId = dto.PowerBIReportId.Trim();
        await _clientRepository.UpdateAsync(client, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Updated client {ClientId}", id);
        return MapToDto(client);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var client = await _clientRepository.GetByIdAsync(id, cancellationToken);
        if (client == null || client.IsDeleted) return false;
        client.IsDeleted = true;
        client.IsActive = false;
        await _clientRepository.UpdateAsync(client, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deleted client {ClientId}", id);
        return true;
    }

    public async Task<bool> AssignUserToClientAsync(Guid userId, Guid clientId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        var client = await _clientRepository.GetByIdAsync(clientId, cancellationToken);
        if (user == null || user.IsDeleted || client == null || client.IsDeleted)
            return false;
        user.ClientId = clientId;
        await _userRepository.UpdateAsync(user, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Assigned user {UserId} to client {ClientId} ({ClientCode})", userId, clientId, client.ClientCode);
        return true;
    }

    public async Task<string?> GetClientCodeForUserEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var user = await _userRepository.GetByPredicateAsync(u => u.Email == email.Trim().ToLowerInvariant() && !u.IsDeleted, cancellationToken);
        if (user?.ClientId != null)
        {
            var client = await _clientRepository.GetByIdAsync(user.ClientId.Value, cancellationToken);
            if (client is { IsDeleted: false })
                return client.ClientCode ?? client.BlobFolderPath;
        }
        var fromCompany = await _clientByCompanyQuery.GetClientForUserEmailAsync(email, cancellationToken);
        return fromCompany?.ClientCode;
    }

    private static ClientDto MapToDto(Client c)
    {
        return new ClientDto
        {
            ClientId = c.Id,
            ClientCode = c.ClientCode,
            ClientName = c.ClientName,
            Industry = c.Industry,
            BlobFolderPath = c.BlobFolderPath,
            TemplateVersion = c.TemplateVersion,
            IsActive = c.IsActive,
            CreatedDate = c.CreatedDate,
            PowerBIWorkspaceId = c.PowerBIWorkspaceId,
            PowerBIDatasetId = c.PowerBIDatasetId,
            PowerBIReportId = c.PowerBIReportId
        };
    }
}
