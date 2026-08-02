namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// Short-lived scratch storage for the source file a single Report Validation run needs to
/// re-drive the report generator wizard via Playwright. Deliberately NOT a durable "report by
/// ID" store — the blob is deleted once the run completes (see ReportValidationBackgroundService),
/// mirroring IBlueprintStorageService's Azure-Blob-with-local-fallback pattern but scoped to one
/// run's lifetime rather than a versioned artefact history.
/// </summary>
public interface IReportValidationScratchStorageService
{
    /// <summary>Persists the uploaded source file bytes for one validation run. Returns the
    /// relative blob path to store on ReportValidationRun.SourceFileScratchBlobPath.</summary>
    Task<string> StoreAsync(Guid runId, string fileName, Stream fileStream, CancellationToken cancellationToken = default);

    /// <returns>A readable stream, or null if the blob does not exist (e.g. already deleted).</returns>
    Task<Stream?> GetAsync(string blobPath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default);
}
