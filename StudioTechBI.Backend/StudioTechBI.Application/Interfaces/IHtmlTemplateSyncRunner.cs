namespace StudioTechBI.Application.Interfaces;

public record HtmlTemplateSyncResult(int Total, int BlobResolved, int SeedFallback);

/// <summary>
/// The actual "read templates/html/index.json + every listed manifest, push the registry to
/// ReportAgent.Api" logic, extracted out of HtmlTemplateRegistrySyncService's timer loop so it can
/// also be triggered on demand (e.g. an admin's "Sync Now" action right after an upload/edit)
/// without waiting for the next scheduled cycle.
/// </summary>
public interface IHtmlTemplateSyncRunner
{
    Task<HtmlTemplateSyncResult> RunOnceAsync(CancellationToken cancellationToken = default);
}
