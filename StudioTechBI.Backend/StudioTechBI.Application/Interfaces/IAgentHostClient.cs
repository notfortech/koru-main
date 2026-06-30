using StudioTechBI.Application.DTOs.Blueprints;

namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// Low-level HTTP client for STBI-AgentHost. Koru must never know about AI models
/// or prompt engineering — this interface is the only integration point.
/// </summary>
public interface IAgentHostClient
{
    Task<BlueprintGenerationResponse> GenerateBlueprintAsync(
        GenerateBlueprintRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}
