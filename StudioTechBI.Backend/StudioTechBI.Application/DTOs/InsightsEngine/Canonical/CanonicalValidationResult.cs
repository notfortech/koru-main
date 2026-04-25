namespace StudioTechBI.Application.DTOs.InsightsEngine.Canonical;

public sealed class CanonicalValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

