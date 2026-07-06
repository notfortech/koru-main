using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.ReportDesigner;
using StudioTechBI.Application.Utilities;

namespace StudioTechBI.Infrastructure.Services;

/// <summary>
/// Reads structural schema metadata from SQL Server using GetSchema() — read-only,
/// no SELECT statements, no data rows. Connection closes immediately after schema read.
/// </summary>
public sealed class SqlSchemaReaderService
{
    private readonly ILogger<SqlSchemaReaderService> _logger;

    public SqlSchemaReaderService(ILogger<SqlSchemaReaderService> logger)
    {
        _logger = logger;
    }

    public async Task<ExtractedSchemaDto> ReadSchemaAsync(
        SqlConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        // Build connection string — password is never logged
        var connStr = BuildConnectionString(request);

        _logger.LogInformation(
            "SqlSchemaReader.Connecting Host={Host} Database={Database}",
            request.Host, request.Database);

        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(cancellationToken);

        _logger.LogInformation(
            "SqlSchemaReader.Connected ServerVersion={ServerVersion} Database={Database}",
            conn.ServerVersion, conn.Database);

        // GetSchema reads metadata only — no SELECT, no data rows
        var tablesSchema = conn.GetSchema("Tables");
        var columnsSchema = conn.GetSchema("Columns");

        await conn.CloseAsync();

        _logger.LogInformation(
            "SqlSchemaReader.Disconnected — schema read complete, connection closed.");

        // Build table map
        var tableMap = new Dictionary<string, List<ColumnSchemaDto>>(StringComparer.OrdinalIgnoreCase);

        foreach (System.Data.DataRow row in tablesSchema.Rows)
        {
            var tableType = row["TABLE_TYPE"]?.ToString();
            if (tableType != "BASE TABLE") continue;

            var tableName = row["TABLE_NAME"]?.ToString();
            if (string.IsNullOrWhiteSpace(tableName)) continue;

            tableMap[tableName] = new List<ColumnSchemaDto>();
        }

        foreach (System.Data.DataRow row in columnsSchema.Rows)
        {
            var tableName = row["TABLE_NAME"]?.ToString();
            if (string.IsNullOrWhiteSpace(tableName) || !tableMap.ContainsKey(tableName)) continue;

            var colName = row["COLUMN_NAME"]?.ToString() ?? "Column";
            var dataType = MapSqlType(row["DATA_TYPE"]?.ToString());
            var nullable = row["IS_NULLABLE"]?.ToString() == "YES";
            int? maxLen = null;
            if (int.TryParse(row["CHARACTER_MAXIMUM_LENGTH"]?.ToString(), out var ml) && ml > 0)
                maxLen = ml;

            tableMap[tableName].Add(new ColumnSchemaDto(colName, dataType, nullable, maxLen));
        }

        var tables = tableMap
            .Where(kv => kv.Value.Count > 0)
            .Select(kv => new TableSchemaDto(
                TableName: kv.Key,
                SheetName: null,
                RowCount: 0,  // row count not fetched — no SELECT statements
                Columns: kv.Value))
            .OrderBy(t => t.TableName)
            .ToList();

        if (tables.Count == 0)
            throw new InvalidOperationException(
                $"No tables found in database '{request.Database}'. Verify the connection and permissions.");

        var allColumns = tables.SelectMany(t => t.Columns.Select(c => $"{t.TableName}.{c.ColumnName}")).ToList();
        var schemaHash = SchemaHashHelper.ComputeSchemaHash(allColumns);

        _logger.LogInformation(
            "SqlSchemaReader.SchemaExtracted Database={Database} TableCount={TableCount} ColumnCount={ColumnCount}",
            request.Database, tables.Count, allColumns.Count);

        return new ExtractedSchemaDto(
            Source: "sql",
            FileName: $"{request.Host}/{request.Database}",
            Tables: tables,
            SchemaHash: schemaHash,
            ExtractedAt: DateTimeOffset.UtcNow);
    }

    private static string BuildConnectionString(SqlConnectionRequest r) =>
        new SqlConnectionStringBuilder
        {
            DataSource = r.Port > 0 ? $"{r.Host},{r.Port}" : r.Host,
            InitialCatalog = r.Database,
            UserID = r.Username,
            Password = r.Password,
            Encrypt = true,
            TrustServerCertificate = false,
            ConnectTimeout = 10,
            ApplicationName = "StudioTechBI-ReportDesigner"
        }.ToString();

    private static string MapSqlType(string? sqlType) => (sqlType ?? "").ToLowerInvariant() switch
    {
        "int" or "bigint" or "smallint" or "tinyint" => "integer",
        "decimal" or "numeric" or "float" or "real" or "money" or "smallmoney" => "decimal",
        "datetime" or "datetime2" or "date" or "time" or "datetimeoffset" or "smalldatetime" => "datetime",
        "bit" => "boolean",
        _ => "string"
    };
}
