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
    /// uploads/init + uploads/{id}/complete), deliberately separate from and larger than
    /// <see cref="MaxUploadBytes"/> -- that value is the sync-path/routing threshold (below it,
    /// the frontend uses the fast synchronous /generate call unchanged), not a hard ceiling on
    /// what the system can accept. Reusing MaxUploadBytes here would defeat the entire point of
    /// the async path (supporting files bigger than the sync limit). Set generously since the
    /// real per-file processing ceiling is enforced downstream, independently, by
    /// DashboardAgents.ReportAgent.Api's own MaxInputFileBytes setting (see that service's
    /// Program.cs) -- a job whose file exceeds that limit fails cleanly with a clear error on the
    /// ReportGenerationJob row, it doesn't silently corrupt or hang.</summary>
    public long MaxAsyncUploadBytes { get; set; } = 500L * 1024L * 1024L; // 500 MB
}
