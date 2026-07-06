using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
using StudioTechBI.Application.DTOs.Connectors;
using StudioTechBI.Application.DTOs.ReportDesigner;
using StudioTechBI.Application.Models;
using StudioTechBI.Application.Utilities;

namespace StudioTechBI.Infrastructure.Services;

/// <summary>
/// Browses SharePoint site file trees and extracts schema metadata from Excel/CSV files.
/// Uses app-only Graph authentication (AzureAd service account) — no user OAuth required.
/// Files are streamed and discarded immediately after schema extraction; no data is persisted.
/// </summary>
public sealed class SharePointSchemaService
{
    private static readonly string[] SupportedExtensions = { ".xlsx", ".xls", ".csv" };

    private readonly AzureAdOptions _azureAd;
    private readonly ILogger<SharePointSchemaService> _logger;

    public SharePointSchemaService(
        IOptions<AzureAdOptions> azureAdOptions,
        ILogger<SharePointSchemaService> logger)
    {
        _azureAd = azureAdOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Lists folders and supported data files (xlsx/xls/csv) at the root of the site's default drive.
    /// </summary>
    public async Task<List<FileItem>> BrowseAsync(
        string siteUrl,
        CancellationToken cancellationToken = default)
    {
        var graph = CreateAppOnlyClient();
        var site = await ResolveSiteAsync(graph, siteUrl, cancellationToken);

        _logger.LogInformation(
            "SharePoint.Browse SiteId={SiteId} SiteUrl={SiteUrl}",
            site.Id, siteUrl);

        var drive = await graph.Sites[site.Id].Drive.GetAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Could not resolve the default document library for this SharePoint site.");

        var result = await graph.Drives[drive.Id].Items["root"].Children.GetAsync(r =>
        {
            r.QueryParameters.Top = 200;
            r.QueryParameters.Select = new[] { "id", "name", "file", "folder", "size" };
        }, cancellationToken);

        var items = result?.Value ?? new List<Microsoft.Graph.Models.DriveItem>();

        return items
            .Where(i => i.Folder != null || IsSupportedFile(i.Name))
            .Select(i => new FileItem
            {
                Id = i.Id ?? string.Empty,
                Name = i.Name ?? i.Id ?? string.Empty,
                MimeType = i.Folder != null ? "folder" : i.File?.MimeType
            })
            .OrderBy(f => f.MimeType == "folder" ? 0 : 1)
            .ThenBy(f => f.Name)
            .ToList();
    }

    /// <summary>
    /// Downloads a SharePoint file by drive item ID, extracts schema metadata, and discards the stream.
    /// No data values are returned — column names, types, and row counts only.
    /// </summary>
    public async Task<ExtractedSchemaDto> ExtractSchemaAsync(
        string siteUrl,
        string driveItemId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var graph = CreateAppOnlyClient();
        var site = await ResolveSiteAsync(graph, siteUrl, cancellationToken);
        var drive = await graph.Sites[site.Id].Drive.GetAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Could not resolve default drive for this SharePoint site.");

        _logger.LogInformation(
            "SharePoint.ExtractSchema SiteId={SiteId} DriveItemId={DriveItemId} FileName={FileName}",
            site.Id, driveItemId, fileName);

        Stream? fileStream = null;
        try
        {
            fileStream = await graph.Drives[drive.Id].Items[driveItemId].Content
                .GetAsync(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException($"Graph returned no content for item '{driveItemId}'.");

            // Buffer into memory so we can seek; dispose original stream immediately
            var memory = new MemoryStream();
            await fileStream.CopyToAsync(memory, cancellationToken);
            memory.Position = 0;

            TableSchemaDto table;
            if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                var (cols, _) = await CsvSampleExtractor.ExtractAsync(memory, 1, cancellationToken);
                table = new TableSchemaDto(
                    TableName: Path.GetFileNameWithoutExtension(fileName),
                    SheetName: Path.GetFileNameWithoutExtension(fileName),
                    RowCount: 0,
                    Columns: cols.Select(c => new ColumnSchemaDto(c, "string", true, null)).ToList());
            }
            else
            {
                memory.Position = 0;
                table = await ExcelSchemaExtractor.ExtractAsync(memory, fileName, cancellationToken);
            }

            var schemaHash = SchemaHashHelper.ComputeSchemaHash(table.Columns.Select(c => c.ColumnName).ToList());

            _logger.LogInformation(
                "SharePoint.SchemaExtracted FileName={FileName} Columns={ColumnCount} Rows={RowCount}",
                fileName, table.Columns.Count, table.RowCount);

            return new ExtractedSchemaDto(
                Source: "sharepoint",
                FileName: fileName,
                Tables: new List<TableSchemaDto> { table },
                SchemaHash: schemaHash,
                ExtractedAt: DateTimeOffset.UtcNow);
        }
        catch (ODataError ex)
        {
            _logger.LogError(ex, "SharePoint Graph error extracting schema for {DriveItemId}: {Code} {Message}",
                driveItemId, ex.Error?.Code, ex.Error?.Message);
            throw new InvalidOperationException(
                $"Failed to access SharePoint file '{fileName}'. Check site permissions and the item ID.", ex);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "SharePoint Graph API error for {DriveItemId}: {Message}", driveItemId, ex.Message);
            throw new InvalidOperationException(
                $"Failed to access SharePoint file '{fileName}'.", ex);
        }
        finally
        {
            if (fileStream != null) await fileStream.DisposeAsync();
        }
    }

    private GraphServiceClient CreateAppOnlyClient()
    {
        if (string.IsNullOrWhiteSpace(_azureAd.TenantId) ||
            string.IsNullOrWhiteSpace(_azureAd.ClientId) ||
            string.IsNullOrWhiteSpace(_azureAd.ClientSecret))
            throw new InvalidOperationException(
                "AzureAd:TenantId, AzureAd:ClientId, and AzureAd:ClientSecret must be configured for SharePoint access.");

        var credential = new ClientSecretCredential(
            _azureAd.TenantId,
            _azureAd.ClientId,
            _azureAd.ClientSecret);

        return new GraphServiceClient(credential,
            new[] { "https://graph.microsoft.com/.default" });
    }

    private static async Task<Microsoft.Graph.Models.Site> ResolveSiteAsync(
        GraphServiceClient graph,
        string siteUrl,
        CancellationToken ct)
    {
        if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out var uri))
            throw new ArgumentException($"'{siteUrl}' is not a valid URL.", nameof(siteUrl));

        // Graph: GET /sites/{hostname}:/{server-relative-path}
        var hostName = uri.Host;
        var sitePath = uri.AbsolutePath.TrimStart('/');

        var site = await graph.Sites[$"{hostName}:/{sitePath}"].GetAsync(cancellationToken: ct)
            ?? throw new InvalidOperationException($"Could not resolve SharePoint site at '{siteUrl}'.");

        return site;
    }

    private static bool IsSupportedFile(string? name) =>
        name != null && SupportedExtensions.Any(ext =>
            name.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
}
