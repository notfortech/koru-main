namespace StudioTechBI.Application.DTOs.ReportDesigner;

public record ExtractedSchemaDto(
    string Source,
    string FileName,
    List<TableSchemaDto> Tables,
    string SchemaHash,
    DateTimeOffset ExtractedAt);

public record TableSchemaDto(
    string TableName,
    string? SheetName,
    int RowCount,
    List<ColumnSchemaDto> Columns);

public record ColumnSchemaDto(
    string ColumnName,
    string DataType,
    bool IsNullable,
    int? MaxLength);
