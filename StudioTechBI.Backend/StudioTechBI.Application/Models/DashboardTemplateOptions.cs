namespace StudioTechBI.Application.Models;

/// <summary>
/// Where the Dashboard Template Generator (DashboardTemplateController) publishes generated
/// reports. Today the whole platform operates against a single shared Power BI workspace — the
/// same one the existing embedded sample report already lives in — rather than one workspace per
/// client. Per-client/per-tenant separation is a planned future enhancement (e.g. dedicated
/// workspaces/capacity for premium clients who need it regularly), not built yet. Because every
/// generation targets this one workspace, differentiating between clients/generations happens
/// entirely through naming (see DashboardTemplateController's timestamped report display name)
/// rather than workspace isolation.
/// </summary>
public class DashboardTemplateOptions
{
    public const string SectionName = "DashboardTemplate";

    /// <summary>Set this to the real, already-existing workspace name in appsettings/env
    /// (DashboardTemplate:WorkspaceName) — the default here is a placeholder.</summary>
    public string WorkspaceName { get; set; } = "StudioTechBI Reports";
}
