namespace StudioTechBI.Application.DTOs.Insight;

public sealed class GenerateFromBlobRequest
{
    public Guid ClientId { get; set; }
    public string BlobPath { get; set; } = "";
}

