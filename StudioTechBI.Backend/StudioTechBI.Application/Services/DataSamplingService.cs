using StudioTechBI.Application.DTOs.Insight;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Utilities;

namespace StudioTechBI.Application.Services;

public sealed class DataSamplingService
{
    private readonly IBlobStorageService _blobStorage;

    public DataSamplingService(IBlobStorageService blobStorage)
    {
        _blobStorage = blobStorage;
    }

    public sealed record SampleResult(
        List<string> Columns,
        List<Dictionary<string, string>> Rows,
        string SchemaHash);

    /// <summary>
    /// Returns a first-N-row sample for supported formats.
    /// Currently supports CSV. For other formats, returns null.
    /// </summary>
    public async Task<SampleResult?> GetSampleAsync(Stream stream, string fileNameOrPath, int maxRows = 100, CancellationToken cancellationToken = default)
    {
        var name = (fileNameOrPath ?? "").Trim();
        if (name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            var (cols, rows) = await CsvSampleExtractor.ExtractAsync(stream, maxRows, cancellationToken);
            var hash = SchemaHashHelper.ComputeSchemaHash(cols);
            return new SampleResult(cols, rows, hash);
        }

        if (name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var (cols, rows) = await ExcelSampleExtractor.ExtractAsync(stream, maxRows, cancellationToken);
            var hash = SchemaHashHelper.ComputeSchemaHash(cols);
            return new SampleResult(cols, rows, hash);
        }

        return null;
    }

    /// <summary>
    /// Downloads the blob and returns a max-100-row sample suitable for the InsightEngine recommendation endpoint.
    /// </summary>
    public async Task<SampleRequest> CreateSampleAsync(string blobPath, Guid clientId, int maxRows = 100, CancellationToken cancellationToken = default)
    {
        var path = (blobPath ?? "").Trim().Replace("\\", "/");
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("BlobPath is required.");

        await using var stream = await _blobStorage.DownloadBlobAsync(path, cancellationToken)
            ?? throw new InvalidOperationException($"Blob not found: {path}");

        var sample = await GetSampleAsync(stream, path, maxRows, cancellationToken);
        if (sample == null)
            throw new NotSupportedException("Unsupported file type. Only .csv and .xlsx are supported for sampling.");

        return new SampleRequest
        {
            ClientId = clientId,
            BlobPath = path,
            Columns = sample.Columns,
            SampleRows = sample.Rows
                .Take(maxRows)
                .Select(r => r.ToDictionary(k => k.Key, v => (object)(v.Value ?? ""), StringComparer.OrdinalIgnoreCase))
                .ToList()
        };
    }
}

