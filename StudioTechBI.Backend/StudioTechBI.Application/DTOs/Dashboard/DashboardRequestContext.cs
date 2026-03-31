namespace StudioTechBI.Application.DTOs.Dashboard;

/// <summary>Resolved caller context built in the API layer (no claims object in application service).</summary>
public sealed class DashboardRequestContext
{
    public required string? Email { get; init; }

    /// <summary>True when user should see accountant/firm analytics (Accountant or Admin role on portal user JWT).</summary>
    public bool IsAccountantView { get; init; }

    /// <summary>True when user should see client-simplified analytics.</summary>
    public bool IsClientView { get; init; }

    /// <summary>Client IDs the user may access (single or many).</summary>
    public IReadOnlyList<Guid> ScopedClientIds { get; init; } = Array.Empty<Guid>();
}
