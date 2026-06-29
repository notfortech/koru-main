namespace StudioTechBI.Domain.Entities;

public static class BlueprintStatuses
{
    // Blueprint aggregate status
    public const string Active = "Active";
    public const string Archived = "Archived";

    // BlueprintGeneration job status
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}
