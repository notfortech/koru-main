namespace StudioTechBI.Application.Models;

/// <summary>Subset of DataConnection.ConfigJson for single-fetch semantics.</summary>
public class DataConnectionImportState
{
    public string? LastImportedFileId { get; set; }
    public string? LastBlobPath { get; set; }
    public DateTime? ImportedAtUtc { get; set; }
}

public class DataConnectionConfigPayload
{
    public DataConnectionImportState? LastImport { get; set; }
}
