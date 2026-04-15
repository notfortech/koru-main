using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.Connectors;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Utilities;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Connectors;

public class GoogleDriveConnector : IDataConnector
{
    private readonly ILogger<GoogleDriveConnector> _logger;

    public GoogleDriveConnector(ILogger<GoogleDriveConnector> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<FileItem>> ListFilesAsync(DataConnection connection, CancellationToken cancellationToken = default)
    {
        var service = CreateDriveService(connection);
        var list = service.Files.List();
        list.PageSize = 20;
        list.Fields = "files(id,name,mimeType),nextPageToken";
        list.Q = "trashed = false";

        var result = await list.ExecuteAsync(cancellationToken);
        return (result.Files ?? new List<Google.Apis.Drive.v3.Data.File>())
            .Select(f => new FileItem
            {
                Id = f.Id ?? "",
                Name = f.Name ?? "",
                MimeType = f.MimeType
            })
            .Where(f => f.Id.Length > 0)
            .ToList();
    }

    public async Task<Stream> DownloadFileAsync(DataConnection connection, string fileId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileId))
            throw new ArgumentException("File id is required.", nameof(fileId));

        var service = CreateDriveService(connection);
        var meta = await service.Files.Get(fileId).ExecuteAsync(cancellationToken);
        var mime = meta.MimeType ?? "";

        var stream = new MemoryStream();

        if (mime.Contains("google-apps.spreadsheet", StringComparison.OrdinalIgnoreCase))
        {
            await service.Files.Export(fileId, "text/csv").DownloadAsync(stream, cancellationToken);
        }
        else if (mime.Contains("google-apps.document", StringComparison.OrdinalIgnoreCase))
        {
            await service.Files.Export(fileId, "text/plain").DownloadAsync(stream, cancellationToken);
        }
        else
        {
            await service.Files.Get(fileId).DownloadAsync(stream, cancellationToken);
        }

        stream.Position = 0;
        _logger.LogInformation("Downloaded Google Drive file {FileId} ({Mime})", fileId, mime);
        return stream;
    }

    public async Task<List<Dictionary<string, object>>> GetPreviewAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        var (_, rows) = await CsvSampleExtractor.ExtractAsync(fileStream, CsvSampleExtractor.DefaultMaxRows, cancellationToken);
        return rows.Select(r => r.ToDictionary(kv => kv.Key, kv => (object)kv.Value)).ToList();
    }

    private static DriveService CreateDriveService(DataConnection connection)
    {
        var token = connection.AccessToken?.Trim();
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException("Google Drive connection has no access token.");

        var credential = GoogleCredential.FromAccessToken(token);
        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "StudioTechBI"
        });
    }
}
