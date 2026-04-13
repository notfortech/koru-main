namespace StudioTechBI.Application.DTOs.Connectors;

public class ConnectorFilesResponseDto
{
    public bool Success { get; set; } = true;
    public List<FileItem> Files { get; set; } = new();
}

public class ConnectorImportResponseDto
{
    public bool Success { get; set; } = true;
    public string BlobPath { get; set; } = string.Empty;
}
