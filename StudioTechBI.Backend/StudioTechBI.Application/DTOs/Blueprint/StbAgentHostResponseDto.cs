using System.Text.Json;
using System.Text.Json.Serialization;

namespace StudioTechBI.Application.DTOs.Blueprint;

/// <summary>Response from POST /api/blueprints/generate on STBI AgentHost.</summary>
public sealed class StbAgentHostResponseDto
{
    public Guid RequestId { get; set; }

    /// <summary>Completed | PartiallyValid | Failed</summary>
    public string Status { get; set; } = string.Empty;

    public string? Provider { get; set; }
    public string? Model { get; set; }
    public int ProcessingTimeMs { get; set; }
    public int Confidence { get; set; }
    public string? SavedFilePath { get; set; }

    /// <summary>Secure URL: GET /api/blueprints/{requestId}/pdf on AgentHost.</summary>
    public string? PdfDownloadUrl { get; set; }

    public int CreditsRemaining { get; set; }
    public int CreditsConsumed { get; set; }
    public DateTimeOffset? ResetDate { get; set; }
    public string? SubscriptionPlan { get; set; }

    /// <summary>Full blueprint JSON — stored to Azure Blob for AI training.</summary>
    [JsonPropertyName("blueprint")]
    public JsonElement? Blueprint { get; set; }

    public List<string> Warnings { get; set; } = new();
}
