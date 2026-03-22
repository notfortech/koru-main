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
}

