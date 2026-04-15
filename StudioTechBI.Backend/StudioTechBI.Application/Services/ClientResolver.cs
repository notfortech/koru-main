using Microsoft.Extensions.Logging;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class ClientResolver : IClientResolver
{
    private readonly IRepository<Client> _clientRepository;
    private readonly ILogger<ClientResolver> _logger;

    public ClientResolver(IRepository<Client> clientRepository, ILogger<ClientResolver> logger)
    {
        _clientRepository = clientRepository;
        _logger = logger;
    }

    public async Task<Client?> ResolveAsync(string clientIdOrCode, CancellationToken cancellationToken = default)
    {
        var value = (clientIdOrCode ?? "").Trim();
        if (value.Length == 0)
            return null;

        if (Guid.TryParse(value, out var guid))
        {
            var byId = await _clientRepository.GetByIdAsync(guid, cancellationToken);
            if (byId != null) return byId;
        }

        // Common frontend value is ClientCode e.g. "AU-004"
        var byCode = await _clientRepository.FirstOrDefaultAsync(
            c => !c.IsDeleted && c.IsActive && c.ClientCode != null && c.ClientCode == value,
            cancellationToken);
        if (byCode != null) return byCode;

        // Fallback to blob folder (some code paths use it as identifier)
        var byBlobFolder = await _clientRepository.FirstOrDefaultAsync(
            c => !c.IsDeleted && c.IsActive && c.BlobFolderPath != null && c.BlobFolderPath == value,
            cancellationToken);
        if (byBlobFolder != null) return byBlobFolder;

        _logger.LogWarning("ClientResolver: could not resolve client identifier '{ClientIdOrCode}'.", value);
        return null;
    }
}

