using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Connectors;

public class DataConnectorRegistry : IDataConnectorRegistry
{
    private readonly IReadOnlyDictionary<string, IDataConnector> _connectors;

    public DataConnectorRegistry(
        GoogleDriveConnector googleDrive,
        OneDriveConnector oneDrive,
        SharePointConnector sharePoint)
    {
        _connectors = new Dictionary<string, IDataConnector>(StringComparer.OrdinalIgnoreCase)
        {
            [DataConnectionTypes.GoogleDrive] = googleDrive,
            [DataConnectionTypes.OneDrive] = oneDrive,
            [DataConnectionTypes.SharePoint] = sharePoint
        };
    }

    public IDataConnector Get(string connectionType)
    {
        if (string.IsNullOrWhiteSpace(connectionType)
            || !_connectors.TryGetValue(connectionType.Trim(), out var connector))
        {
            throw new InvalidOperationException(
                $"Unknown data connection type '{connectionType}'. Expected one of: GoogleDrive, OneDrive, SharePoint.");
        }

        return connector;
    }
}
