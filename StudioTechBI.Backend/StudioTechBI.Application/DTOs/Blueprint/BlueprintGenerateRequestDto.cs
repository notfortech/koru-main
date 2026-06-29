namespace StudioTechBI.Application.DTOs.Blueprint;

public sealed class BlueprintGenerateRequestDto
{
    /// <summary>Natural language description of what the dashboards need to achieve.</summary>
    public string BusinessRequirement { get; set; } = string.Empty;

    /// <summary>Vertical/sector, e.g. "Property Management". Defaults to client's Industry if omitted.</summary>
    public string? Industry { get; set; }

    /// <summary>Optional JSON describing existing data columns and types. Do NOT include sample row values.</summary>
    public string? ExistingSchema { get; set; }

    /// <summary>When true, clientCode is used; otherwise the JWT client_code claim is used.</summary>
    public bool UseSelectedClient { get; set; }

    public string? ClientCode { get; set; }
}
