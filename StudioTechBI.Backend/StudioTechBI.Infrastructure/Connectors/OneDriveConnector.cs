using Microsoft.Graph;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.Connectors;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Utilities;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Connectors;

public class OneDriveConnector : IDataConnector
{
    private readonly MicrosoftGraphClientFactory _graphFactory;
    private readonly ILogger<OneDriveConnector> _logger;

    public OneDriveConnector(MicrosoftGraphClientFactory graphFactory, ILogger<OneDriveConnector> logger)
    {
        _graphFactory = graphFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FileItem>> ListFilesAsync(DataConnection connection, CancellationToken cancellationToken = default)
    {
        EnsureTokenUsable(connection);

        _logger.LogInformation("Listing files for OneDrive (connection type {Type}).", connection.Type);

        var graphClient = _graphFactory.Create(connection.AccessToken!);

        try
        {
            var driveId = await ResolveDefaultDriveIdAsync(graphClient, cancellationToken);

            var result = await graphClient.Drives[driveId].Items["root"].Children.GetAsync(
                requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Top = 200;
                    requestConfiguration.QueryParameters.Select = new[] { "id", "name", "file", "folder", "size" };
                },
                cancellationToken);

            var page = result?.Value;
            if (page == null || !page.Any())
                return Array.Empty<FileItem>();

            return page
                .Where(i => !string.IsNullOrEmpty(i.Id))
                .Select(i => new FileItem
                {
                    Id = i.Id!,
                    Name = i.Name ?? i.Id!,
                    MimeType = i.File?.MimeType
                })
                .ToList();
        }
        catch (ODataError ex)
        {
            _logger.LogError(ex, "Microsoft Graph OData error listing OneDrive: {Code} {Message}", ex.Error?.Code, ex.Error?.Message);
            throw new InvalidOperationException(
                "Failed to list OneDrive files via Microsoft Graph. Check token scopes (Files.Read, Files.Read.All) and permissions.",
                ex);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Microsoft Graph error while listing OneDrive files: {Message}", ex.Message);
            throw new InvalidOperationException(
                "Failed to list OneDrive files via Microsoft Graph. Check token scopes (Files.Read, Files.Read.All) and permissions.",
                ex);
        }
    }

    public async Task<Stream> DownloadFileAsync(DataConnection connection, string fileId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileId))
            throw new ArgumentException("File id is required.", nameof(fileId));

        EnsureTokenUsable(connection);

        _logger.LogInformation("Downloading OneDrive file {FileId}.", fileId);

        var graphClient = _graphFactory.Create(connection.AccessToken!);

        try
        {
            var driveId = await ResolveDefaultDriveIdAsync(graphClient, cancellationToken);

            var stream = await graphClient.Drives[driveId].Items[fileId].Content.GetAsync(cancellationToken: cancellationToken);
            if (stream == null)
                throw new InvalidOperationException("Microsoft Graph returned an empty response for file content.");

            var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            await stream.DisposeAsync();
            memory.Position = 0;

            _logger.LogInformation("Downloaded OneDrive file {FileId}, size {Length} bytes.", fileId, memory.Length);
            return memory;
        }
        catch (ODataError ex)
        {
            _logger.LogError(ex, "Microsoft Graph OData error downloading OneDrive file {FileId}: {Code} {Message}", fileId, ex.Error?.Code, ex.Error?.Message);
            throw new InvalidOperationException(
                $"Failed to download OneDrive item '{fileId}'. Ensure the id is a file (not a folder) and the token has read access.",
                ex);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Microsoft Graph error while downloading OneDrive file {FileId}: {Message}", fileId, ex.Message);
            throw new InvalidOperationException(
                $"Failed to download OneDrive item '{fileId}'. Ensure the id is a file (not a folder) and the token has read access.",
                ex);
        }
    }

    public async Task<List<Dictionary<string, object>>> GetPreviewAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        var (_, rows) = await CsvSampleExtractor.ExtractAsync(fileStream, CsvSampleExtractor.DefaultMaxRows, cancellationToken);
        return rows.Select(r => r.ToDictionary(kv => kv.Key, kv => (object)kv.Value)).ToList();
    }

    private static void EnsureTokenUsable(DataConnection connection)
    {
        var token = connection.AccessToken?.Trim();
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException("OneDrive connection has no access token.");

        if (connection.ExpiresAt.HasValue && connection.ExpiresAt.Value <= DateTime.UtcNow)
            throw new InvalidOperationException(
                "Access token expired. Refresh is not implemented yet; register a new token or extend ExpiresAt after refresh.");
    }

    /// <summary>SDK v5: <c>Me.Drive</c> is GET-only; use returned id with <c>Drives[id].Items[...]</c>.</summary>
    private static async Task<string> ResolveDefaultDriveIdAsync(GraphServiceClient graphClient, CancellationToken cancellationToken)
    {
        var drive = await graphClient.Me.Drive.GetAsync(cancellationToken: cancellationToken);
        if (string.IsNullOrEmpty(drive?.Id))
            throw new InvalidOperationException("Microsoft Graph did not return a default OneDrive id for this user.");

        return drive.Id;
    }
}
