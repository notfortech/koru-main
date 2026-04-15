using System.Text.Json;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.Connectors;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class DataConnectionService : BaseService, IDataConnectionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly IDataConnectionRepository _connectionRepository;
    private readonly IRepository<Client> _clientRepository;
    private readonly IBlobStorageService _blobStorage;
    private readonly IDataConnectorRegistry _connectorRegistry;
    private readonly ILogger<DataConnectionService> _logger;

    public DataConnectionService(
        IUnitOfWork unitOfWork,
        IDataConnectionRepository connectionRepository,
        IRepository<Client> clientRepository,
        IBlobStorageService blobStorage,
        IDataConnectorRegistry connectorRegistry,
        ILogger<DataConnectionService> logger)
        : base(unitOfWork)
    {
        _connectionRepository = connectionRepository;
        _clientRepository = clientRepository;
        _blobStorage = blobStorage;
        _connectorRegistry = connectorRegistry;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DataConnectionSummaryDto>> ListConnectionsForClientAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        var list = await _connectionRepository.GetByClientIdAsync(clientId, cancellationToken);
        return list
            .Select(c => new DataConnectionSummaryDto { Id = c.Id, Type = c.Type })
            .ToList();
    }

    public async Task<DataConnectionDto> RegisterConnectionAsync(RegisterDataConnectionDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.ClientId == Guid.Empty)
            throw new InvalidOperationException("ClientId is required.");

        var type = (dto.Type ?? "").Trim();
        if (string.IsNullOrEmpty(type))
            throw new InvalidOperationException("Connection type is required.");

        _ = _connectorRegistry.Get(type);

        var client = await _clientRepository.GetByIdAsync(dto.ClientId, cancellationToken)
            ?? throw new InvalidOperationException($"Client {dto.ClientId} was not found.");

        var entity = new DataConnection
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Type = type,
            AccessToken = dto.AccessToken?.Trim(),
            RefreshToken = dto.RefreshToken?.Trim(),
            ExpiresAt = dto.ExpiresAt,
            ConfigJson = dto.ConfigJson
        };

        await _connectionRepository.AddAsync(entity, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Registered data connection {ConnectionId} ({Type}) for client {ClientId}.", entity.Id, type, client.Id);

        return new DataConnectionDto
        {
            Id = entity.Id,
            ClientId = entity.ClientId,
            Type = entity.Type,
            ExpiresAt = entity.ExpiresAt
        };
    }

    public async Task<Guid?> GetConnectionClientIdAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionRepository.GetByIdAsync(connectionId, cancellationToken);
        return connection?.ClientId;
    }

    public async Task<IReadOnlyList<FileItem>> ListFilesAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionRepository.GetByIdAsync(connectionId, cancellationToken)
            ?? throw new InvalidOperationException($"Data connection {connectionId} was not found.");

        var connector = _connectorRegistry.Get(connection.Type);
        return await connector.ListFilesAsync(connection, cancellationToken);
    }

    public async Task<string> ImportFileToCreatedBlobAsync(
        Guid connectionId,
        string fileId,
        string? preferredFileName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileId))
            throw new InvalidOperationException("FileId is required.");

        var connection = await _connectionRepository.GetByIdAsync(connectionId, cancellationToken)
            ?? throw new InvalidOperationException($"Data connection {connectionId} was not found.");

        var client = await _clientRepository.GetByIdAsync(connection.ClientId, cancellationToken)
            ?? throw new InvalidOperationException($"Client {connection.ClientId} was not found.");

        var folder = (client.BlobFolderPath ?? client.ClientCode ?? client.Id.ToString()).Trim();
        var payload = DeserializeConfig(connection.ConfigJson);

        if (payload.LastImport != null
            && string.Equals(payload.LastImport.LastImportedFileId, fileId, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(payload.LastImport.LastBlobPath)
            && await _blobStorage.BlobExistsAsync(payload.LastImport.LastBlobPath, cancellationToken))
        {
            _logger.LogInformation(
                "Reusing existing blob for file {FileId}: {BlobPath}",
                fileId,
                payload.LastImport.LastBlobPath);
            return payload.LastImport.LastBlobPath;
        }

        var connector = _connectorRegistry.Get(connection.Type);
        await using var download = await connector.DownloadFileAsync(connection, fileId, cancellationToken);

        var safeName = SanitizeFileName(preferredFileName ?? $"{fileId}.bin");
        var blobPath = $"{folder}/accounting/created/{Guid.NewGuid():N}_{safeName}";

        var contentType = safeName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? "text/csv" : "application/octet-stream";
        await _blobStorage.UploadClientBlobAsync(blobPath, download, contentType, cancellationToken);

        payload.LastImport = new DataConnectionImportState
        {
            LastImportedFileId = fileId,
            LastBlobPath = blobPath,
            ImportedAtUtc = DateTime.UtcNow
        };
        connection.ConfigJson = JsonSerializer.Serialize(payload, JsonOptions);
        await _connectionRepository.UpdateAsync(connection, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Imported connector file {FileId} to blob {BlobPath}.", fileId, blobPath);
        return blobPath;
    }

    private static DataConnectionConfigPayload DeserializeConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new DataConnectionConfigPayload();
        try
        {
            return JsonSerializer.Deserialize<DataConnectionConfigPayload>(json, JsonOptions)
                   ?? new DataConnectionConfigPayload();
        }
        catch
        {
            return new DataConnectionConfigPayload();
        }
    }

    private static string SanitizeFileName(string name)
    {
        var file = Path.GetFileName(name.Trim());
        if (file.Length == 0)
            return "import.bin";

        var chars = file.Where(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-').ToArray();
        var s = new string(chars);
        return s.Length > 0 ? s : "import.bin";
    }
}
