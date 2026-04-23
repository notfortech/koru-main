using System.Text.Json;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.Connectors;
using StudioTechBI.Application.DTOs.Insight;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Application.Utilities;
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
    private readonly IInsightEngineClient _insightEngineClient;
    private readonly IModelRepository _modelRepository;
    private readonly DataSamplingService _sampling;
    private readonly ILogger<DataConnectionService> _logger;

    public DataConnectionService(
        IUnitOfWork unitOfWork,
        IDataConnectionRepository connectionRepository,
        IRepository<Client> clientRepository,
        IBlobStorageService blobStorage,
        IDataConnectorRegistry connectorRegistry,
        IInsightEngineClient insightEngineClient,
        IModelRepository modelRepository,
        DataSamplingService sampling,
        ILogger<DataConnectionService> logger)
        : base(unitOfWork)
    {
        _connectionRepository = connectionRepository;
        _clientRepository = clientRepository;
        _blobStorage = blobStorage;
        _connectorRegistry = connectorRegistry;
        _insightEngineClient = insightEngineClient;
        _modelRepository = modelRepository;
        _sampling = sampling;
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

    public async Task<ConnectorImportResponseDto> ImportFileAndRecommendModelsAsync(
        Guid connectionId,
        string fileId,
        string? preferredFileName,
        CancellationToken cancellationToken = default)
    {
        var blobPath = await ImportFileToCreatedBlobAsync(connectionId, fileId, preferredFileName, cancellationToken);

        // Sample from blob (avoid holding full file in memory).
        await using var blobStream = await _blobStorage.DownloadBlobAsync(blobPath, cancellationToken)
            ?? throw new InvalidOperationException($"Blob not found: {blobPath}");

        string? schemaHash = null;
        List<string>? schemaColumns = null;
        List<Dictionary<string, string>>? previewRows = null;

        var sample = await _sampling.GetSampleAsync(blobStream, blobPath, CsvSampleExtractor.DefaultMaxRows, cancellationToken);
        if (sample != null)
        {
            schemaHash = sample.SchemaHash;
            schemaColumns = sample.Columns;
            previewRows = sample.Rows;
        }

        var request = new GenerateModelRequest
        {
            BlobPath = blobPath,
            ClientId = await GetConnectionClientIdAsync(connectionId, cancellationToken),
            SchemaHash = schemaHash,
            SchemaColumns = schemaColumns,
            PreviewRows = previewRows
        };

        if (request.ClientId == null || request.ClientId == Guid.Empty)
            throw new InvalidOperationException("ClientId not found for connection.");

        var client = await _clientRepository.GetByIdAsync(request.ClientId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Client {request.ClientId} was not found.");

        var folder = (client.BlobFolderPath ?? client.ClientCode ?? client.Id.ToString()).Trim();

        var models = await _insightEngineClient.GenerateModelsAsync(request, cancellationToken);

        foreach (var dto in models)
        {
            var validatedPath = $"{folder}/accounting/validated/{dto.Id:D}/data.xlsx";
            var existing = await _modelRepository.GetByIdAsync(dto.Id, cancellationToken);
            if (existing == null)
            {
                var entity = new InsightModel
                {
                    Id = dto.Id,
                    ClientId = client.Id,
                    TemplateId = dto.TemplateId?.Trim(),
                    Confidence = dto.ResolveConfidence(),
                    Status = InsightWorkflowStatuses.Suggested,
                    MappingJson = dto.MappingJson,
                    ExcelSchemaJson = dto.ExcelSchemaJson,
                    SchemaHash = schemaHash ?? dto.SchemaHash,
                    ValidatedBlobPath = validatedPath,
                    IsFallback = dto.IsFallback ?? false
                };
                await _modelRepository.AddAsync(entity, cancellationToken);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(dto.TemplateId))
                    existing.TemplateId = dto.TemplateId.Trim();
                existing.Confidence = dto.ResolveConfidence();
                existing.Status = InsightWorkflowStatuses.Suggested;
                existing.MappingJson = dto.MappingJson ?? existing.MappingJson;
                existing.ExcelSchemaJson = dto.ExcelSchemaJson ?? existing.ExcelSchemaJson;
                if (schemaHash != null)
                    existing.SchemaHash = schemaHash;
                else if (!string.IsNullOrWhiteSpace(dto.SchemaHash))
                    existing.SchemaHash = dto.SchemaHash;
                existing.ValidatedBlobPath = validatedPath;
                if (dto.IsFallback.HasValue)
                    existing.IsFallback = dto.IsFallback.Value;
                await _modelRepository.UpdateAsync(existing, cancellationToken);
            }
        }

        await UnitOfWork.SaveChangesAsync(cancellationToken);

        return new ConnectorImportResponseDto
        {
            Success = true,
            BlobPath = blobPath,
            Recommendations = models.Select(m => new ModelRecommendationDto
            {
                ModelId = m.Id,
                TemplateId = m.TemplateId,
                Confidence = m.ResolveConfidence()
            }).ToList()
        };
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
