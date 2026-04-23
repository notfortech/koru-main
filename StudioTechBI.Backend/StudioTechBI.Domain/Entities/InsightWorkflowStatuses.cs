namespace StudioTechBI.Domain.Entities;

/// <summary>Insight model lifecycle values.</summary>
public static class InsightWorkflowStatuses
{
    public const string Draft = "Draft";
    public const string Suggested = "Suggested";
    public const string ReadyForSelection = "ReadyForSelection";
    public const string Selected = "Selected";
    public const string Processing = "Processing";
    public const string Active = "Active";
    public const string Failed = "Failed";
    public const string OrchestrationFailed = "OrchestrationFailed";
}
