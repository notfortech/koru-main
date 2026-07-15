namespace StudioTechBI.Application.DTOs.ReportDesigner;

public record SqlConnectionRequest(
    string Host,
    int Port,
    string Database,
    string Username,
    string Password);

public record SharePointBrowseRequest(string SiteUrl);

public record SharePointExtractRequest(
    string SiteUrl,
    string DriveItemId,
    string FileName);

public record GenerateReportModelRequest(
    string ClientId,
    ExtractedSchemaDto Schema,
    string? PreferredTheme);

/// <summary>
/// Records (or declines) a client's consent to send schema metadata for a specific
/// schema shape to the Report Designer AI. Must be called, with ConsentGranted = true,
/// before GenerateReportModelRequest will be accepted for that (ClientId, SchemaHash) pair.
/// </summary>
public record ReportDesignerConsentRequest(
    string ClientId,
    string SchemaHash,
    bool ConsentGranted);
