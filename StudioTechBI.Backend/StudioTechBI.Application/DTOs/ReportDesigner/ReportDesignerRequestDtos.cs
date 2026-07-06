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
    ExtractedSchemaDto Schema,
    string? PreferredTheme);
