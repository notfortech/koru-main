namespace StudioTechBI.Application.DTOs.Connectors;

public class RegisterDataConnectionDto
{
    public Guid ClientId { get; set; }
    /// <summary>GoogleDrive, OneDrive, SharePoint</summary>
    public string Type { get; set; } = string.Empty;
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? ConfigJson { get; set; }
}

public class DataConnectionDto
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
}

public class ImportConnectorFileRequest
{
    public string FileId { get; set; } = string.Empty;
    public string? FileName { get; set; }
}
