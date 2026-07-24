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
    private readonly IRepository<Role> _roleRepository;
    private readonly IRepository<UserRole> _userRoleRepository;
    private readonly IRepository<Company> _companyRepository;
    private readonly IRepository<CompanyUser> _companyUserRepository;
    private readonly IRepository<ClientBlobFolder> _clientBlobFolderRepository;
    private readonly IBlobStorageService _blobStorage;
    private readonly IBlobSasUriProvider _sasUriProvider;
    private readonly ILogger<ClientService> _logger;
    private readonly IClientByCompanyQuery _clientByCompanyQuery;

    private static readonly string[] AllowedLogoExtensions = { ".png", ".jpg", ".jpeg", ".svg" };
    private static readonly TimeSpan LogoSasValidFor = TimeSpan.FromHours(24);

    public ClientService(
        IUnitOfWork unitOfWork,
        IRepository<Client> clientRepository,
        IRepository<User> userRepository,
        IRepository<Role> roleRepository,
        IRepository<UserRole> userRoleRepository,
        IRepository<Company> companyRepository,
        IRepository<CompanyUser> companyUserRepository,
        IRepository<ClientBlobFolder> clientBlobFolderRepository,
        IBlobStorageService blobStorage,
        IBlobSasUriProvider sasUriProvider,
        ILogger<ClientService> logger,
        IClientByCompanyQuery clientByCompanyQuery)
        : base(unitOfWork)
    {
        _clientRepository = clientRepository;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _companyRepository = companyRepository;
        _companyUserRepository = companyUserRepository;
        _clientBlobFolderRepository = clientBlobFolderRepository;
        _blobStorage = blobStorage;
        _sasUriProvider = sasUriProvider;
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
        return await MapToDtoWithLogoAsync(client, cancellationToken);
    }

    public async Task<IReadOnlyList<ClientDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = await _clientRepository.GetAllAsync(cancellationToken);
        var active = list.Where(c => !c.IsDeleted).ToList();
        var dtos = new List<ClientDto>(active.Count);
        foreach (var client in active)
            dtos.Add(await MapToDtoWithLogoAsync(client, cancellationToken));
        return dtos;
    }

    public async Task<ClientDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var client = await _clientRepository.GetByIdAsync(id, cancellationToken);
        return client == null || client.IsDeleted ? null : await MapToDtoWithLogoAsync(client, cancellationToken);
    }

    public async Task<ClientDto?> GetByClientCodeAsync(string clientCode, CancellationToken cancellationToken = default)
    {
        var code = (clientCode ?? "").Trim();
        if (string.IsNullOrEmpty(code)) return null;
        var codeLower = code.ToLowerInvariant();
        var clients = await _clientRepository.FindAsync(c => !c.IsDeleted && c.ClientCode != null && c.ClientCode.ToLower() == codeLower, cancellationToken);
        var client = clients.FirstOrDefault();
        return client == null ? null : await MapToDtoWithLogoAsync(client, cancellationToken);
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
        client.IsPremiumSubscriber = dto.IsPremiumSubscriber;
        client.IsActive = dto.IsActive;
        if (!string.IsNullOrEmpty(client.ClientCode))
            client.BlobFolderPath = client.ClientCode;
        if (dto.PowerBIWorkspaceId != null) client.PowerBIWorkspaceId = dto.PowerBIWorkspaceId.Trim();
        if (dto.PowerBIDatasetId != null) client.PowerBIDatasetId = dto.PowerBIDatasetId.Trim();
        if (dto.PowerBIReportId != null) client.PowerBIReportId = dto.PowerBIReportId.Trim();
        await _clientRepository.UpdateAsync(client, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Updated client {ClientId}", id);
        return await MapToDtoWithLogoAsync(client, cancellationToken);
    }

    public async Task<ClientDto?> SetLogoAsync(Guid id, Stream content, string fileName, string? contentType, CancellationToken cancellationToken = default)
    {
        var client = await _clientRepository.GetByIdAsync(id, cancellationToken);
        if (client == null || client.IsDeleted) return null;

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedLogoExtensions.Contains(ext))
            throw new InvalidOperationException(
                $"Unsupported logo file type '{ext}'. Allowed: {string.Join(", ", AllowedLogoExtensions)}");

        var blobPath = $"{client.Id}/branding/logo{ext}";
        await _blobStorage.UploadClientBlobAsync(blobPath, content, contentType, cancellationToken);

        client.LogoBlobPath = blobPath;
        await _clientRepository.UpdateAsync(client, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Set logo for client {ClientId}", id);

        return await MapToDtoWithLogoAsync(client, cancellationToken);
    }

    public async Task<ClientDto?> ClearLogoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var client = await _clientRepository.GetByIdAsync(id, cancellationToken);
        if (client == null || client.IsDeleted) return null;

        // The blob itself is left in storage rather than deleted -- clearing LogoBlobPath is what
        // actually turns white-labeling off (Logo.tsx falls back to default branding the moment
        // branding.logoUrl is absent); an orphaned blob is harmless and cheap to leave behind
        // rather than adding a new delete-blob code path just for this.
        client.LogoBlobPath = null;
        await _clientRepository.UpdateAsync(client, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Cleared logo for client {ClientId}", id);

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

    public async Task<ClientPhaseOneProvisionResultDto> ProvisionPhaseOneAsync(
        ClientPhaseOneProvisionDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if ((dto.UserId == null || dto.UserId == Guid.Empty) && string.IsNullOrWhiteSpace(dto.UserEmail))
            throw new InvalidOperationException("Provide UserId or UserEmail.");

        User? user = null;
        if (dto.UserId is { } uid && uid != Guid.Empty)
            user = await _userRepository.GetByIdAsync(uid, cancellationToken);
        else if (!string.IsNullOrWhiteSpace(dto.UserEmail))
        {
            var email = dto.UserEmail.Trim().ToLowerInvariant();
            user = await _userRepository.GetByPredicateAsync(u => u.Email == email && !u.IsDeleted, cancellationToken);
        }

        if (user == null || user.IsDeleted)
            throw new InvalidOperationException("User not found. Provide a valid UserId or UserEmail.");

        var clientDto = await CreateAsync(
            new ClientCreateDto
            {
                ClientCode = dto.ClientCode,
                ClientName = dto.ClientName,
                Industry = dto.Industry,
                TemplateVersion = dto.TemplateVersion
            },
            cancellationToken);

        var companyNameResolved = string.IsNullOrWhiteSpace(dto.CompanyName)
            ? $"{dto.ClientName.Trim()} Company"
            : dto.CompanyName.Trim();
        var companyNameStored =
            companyNameResolved.Length > 256 ? companyNameResolved[..256] : companyNameResolved;

        string? industryForCompany = dto.Industry?.Trim();
        if (!string.IsNullOrEmpty(industryForCompany) && industryForCompany.Length > 100)
            industryForCompany = industryForCompany[..100];

        var companyId = Guid.NewGuid();
        await _companyRepository.AddAsync(
            new Company
            {
                Id = companyId,
                Name = companyNameStored,
                ClientId = clientDto.ClientId,
                Industry = industryForCompany,
                BankIntegrationEnabled = false
            },
            cancellationToken);

        var duplicateMembership = await _companyUserRepository.AnyAsync(
            cu => cu.UserId == user.Id && cu.CompanyId == companyId && !cu.IsDeleted,
            cancellationToken);
        if (!duplicateMembership)
        {
            await _companyUserRepository.AddAsync(
                new CompanyUser
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    CompanyId = companyId,
                    Role = 0
                },
                cancellationToken);
        }

        var blobLogicalKey = NormalizeClientCodeSegment(dto.ClientCode);
        var blobLogicalPath = $"clients/{blobLogicalKey}";
        await _clientBlobFolderRepository.AddAsync(
            new ClientBlobFolder
            {
                Id = Guid.NewGuid(),
                ClientId = clientDto.ClientId,
                FolderPath = blobLogicalPath
            },
            cancellationToken);

        user.ClientId = clientDto.ClientId;
        user.IsActive = true;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        var clientRole = await _roleRepository.GetByPredicateAsync(
            r => r.Name == "Client" && !r.IsDeleted,
            cancellationToken);
        if (clientRole == null)
            throw new InvalidOperationException("Role 'Client' was not found in the database (run role seed / migration).");

        var hasClientRole = await _userRoleRepository.AnyAsync(
            ur => ur.UserId == user.Id && ur.RoleId == clientRole.Id && !ur.IsDeleted,
            cancellationToken);
        if (!hasClientRole)
        {
            await _userRoleRepository.AddAsync(
                new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = clientRole.Id
                },
                cancellationToken);
            await UnitOfWork.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Phase 1 provision: client {ClientId} ({ClientCode}), company {CompanyId}, user {UserId} ({Email}).",
            clientDto.ClientId,
            clientDto.ClientCode,
            companyId,
            user.Id,
            user.Email);

        return new ClientPhaseOneProvisionResultDto
        {
            Client = clientDto,
            UserId = user.Id,
            Email = user.Email,
            CompanyId = companyId,
            CompanyName = companyNameStored,
            BlobFolderPath = blobLogicalPath
        };
    }

    /// <summary>Normalizes segment for blob logical paths (slashes, trims).</summary>
    private static string NormalizeClientCodeSegment(string? clientCode)
    {
        var s = (clientCode ?? "").Trim().Replace('\\', '/');
        while (s.Contains("//", StringComparison.Ordinal))
            s = s.Replace("//", "/", StringComparison.Ordinal);
        return s.Trim('/');
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
            IsPremiumSubscriber = c.IsPremiumSubscriber,
            IsActive = c.IsActive,
            CreatedDate = c.CreatedDate,
            PowerBIWorkspaceId = c.PowerBIWorkspaceId,
            PowerBIDatasetId = c.PowerBIDatasetId,
            PowerBIReportId = c.PowerBIReportId
        };
    }

    /// <summary>Same as MapToDto, plus a freshly-signed LogoUrl when the client has a logo set.
    /// SAS generation is a local signing operation (no network call), so doing this per-DTO is cheap.</summary>
    private async Task<ClientDto> MapToDtoWithLogoAsync(Client c, CancellationToken cancellationToken)
    {
        var dto = MapToDto(c);
        if (!string.IsNullOrEmpty(c.LogoBlobPath))
            dto.LogoUrl = await _sasUriProvider.GetReadSasUriAsync(c.LogoBlobPath, LogoSasValidFor, cancellationToken);
        return dto;
    }
}
