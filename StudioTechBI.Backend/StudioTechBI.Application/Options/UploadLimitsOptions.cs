using StudioTechBI.Application.Constants;

namespace StudioTechBI.Application.Options;

/// <summary>
/// Runtime-configurable file-upload size limit, checked inside each file-accepting controller's
/// manual <c>IFormFile.Length</c> check. Defaults to <see cref="UploadLimits.MaxUploadBytes"/>,
/// the same hard ceiling every <c>[RequestSizeLimit]</c> attribute in the API enforces -- set
/// lower via the "UploadLimits:MaxUploadBytes" config key to tighten it further per-environment;
/// raising it above the compile-time ceiling has no effect (Kestrel/the attribute would still
/// reject the request first).
/// </summary>
public sealed class UploadLimitsOptions
{
    public const string SectionName = "UploadLimits";

    public long MaxUploadBytes { get; set; } = UploadLimits.MaxUploadBytes;

    /// <summary>Ceiling for the direct-to-blob async upload path (ReportGeneratorController's
    /// uploads/init + uploads/{id}/complete), separate from <see cref="MaxUploadBytes"/> -- that
    /// value is the sync-path/routing threshold (below it, the frontend uses the fast synchronous
    /// /generate call unchanged), not a hard ceiling on what the system can accept.
    /// Deliberately capped at the same value as DashboardAgents.ReportAgent.Api's own
    /// MaxInputFileBytes setting (see that service's Program.cs; default 60 MB) rather than set
    /// generously above it -- a file the frontend advertises/accepts as fine must actually be
    /// processable, or a client sees a background-job failure after a successful upload instead
    /// of a clear "file too large" message up front. Raise both together, never this alone.</summary>
    public long MaxAsyncUploadBytes { get; set; } = 60L * 1024L * 1024L; // 60 MB -- matches ReportAgent.Api's MaxInputFileBytes default
}
