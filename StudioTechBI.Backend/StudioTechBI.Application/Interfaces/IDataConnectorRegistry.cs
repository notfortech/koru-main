namespace StudioTechBI.Application.Interfaces;

public interface IDataConnectorRegistry
{
    IDataConnector Get(string connectionType);
}
