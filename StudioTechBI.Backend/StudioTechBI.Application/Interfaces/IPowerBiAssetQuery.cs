using StudioTechBI.Application.DTOs.PowerBI;

namespace StudioTechBI.Application.Interfaces;

public interface IPowerBiAssetQuery
{
    /// <summary>
    /// Gets the active Power BI asset for a given client code (e.g. AU-001) and report type (e.g. monthly).
    /// Returns null when no matching active asset exists.
    /// </summary>
    Task<PowerBiAssetDto?> GetActiveAssetByClientCodeAsync(
        string clientCode,
        string reportType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active Power BI asset for a specific catalog template and client.
    /// Prefer rows where <c>ClientId</c> matches; falls back to client-agnostic template rows (<c>ClientId</c> null) if present.
    /// </summary>
    Task<PowerBiAssetDto?> GetActiveAssetByClientCodeAndTemplateIdAsync(
        string clientCode,
        Guid templateId,
        string reportType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all active Power BI assets for a given client code. Used to populate the reports list screen.
    /// </summary>
    Task<IReadOnlyList<PowerBiAssetDto>> GetAllActiveAssetsForClientAsync(
        string clientCode,
        CancellationToken cancellationToken = default);
}

