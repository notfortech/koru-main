using System.Text.Json;
using StudioTechBI.Application.DTOs.DashboardTemplate;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Utilities;

namespace StudioTechBI.Application.Services;

/// <summary>
/// Blends a client's uploaded file with mock data per the requested blueprint's data_model.
/// Known limitation: ExcelSampleExtractor (like ExcelSchemaExtractor) reads a single flat table
/// from the first worksheet — it does not route different sheets to different blueprint tables.
/// So the uploaded file's columns are treated as one flat pool of "available real columns" and
/// matched by name (case-insensitive) against every blueprint table's declared columns, the same
/// flat-matching approach TemplateMatchingService already uses against Template.RequiredColumnsJson.
/// </summary>
public sealed class DataBlendService : IDataBlendService
{
    private const int DefaultMockRowCount = 20;

    public async Task<BlendResult> BlendAsync(Stream uploadedFile, JsonElement blueprint, CancellationToken cancellationToken = default)
    {
        var (realColumns, realRows) = await ExcelSampleExtractor.ExtractAsync(uploadedFile, ExcelSampleExtractor.DefaultMaxRows, cancellationToken);
        var realColumnSet = new HashSet<string>(realColumns, StringComparer.OrdinalIgnoreCase);

        var tables = new List<BlendedTable>();
        var provenance = new List<ProvenanceEntryDto>();

        foreach (var tableDef in EnumerateBlueprintTables(blueprint))
        {
            var (tableName, columns) = tableDef;
            if (columns.Count == 0) continue;

            var tableHasRealColumn = columns.Any(c => realColumnSet.Contains(c.Name));
            var rowCount = tableHasRealColumn ? Math.Max(realRows.Count, 1) : DefaultMockRowCount;

            var blendedColumns = new List<string>();
            var blendedRows = Enumerable.Range(0, rowCount)
                .Select(_ => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
                .ToList();

            foreach (var col in columns)
            {
                blendedColumns.Add(col.Name);

                if (realColumnSet.Contains(col.Name))
                {
                    for (var i = 0; i < rowCount; i++)
                    {
                        var value = i < realRows.Count && realRows[i].TryGetValue(col.Name, out var v) ? v : "";
                        blendedRows[i][col.Name] = value;
                    }
                    provenance.Add(new ProvenanceEntryDto(tableName, col.Name, col.Type, ProvenanceSource.Uploaded, rowCount));
                }
                else
                {
                    var mockValues = MockValueGenerator.Generate(tableName, col.Name, col.Type, rowCount);
                    for (var i = 0; i < rowCount; i++)
                        blendedRows[i][col.Name] = mockValues[i];
                    provenance.Add(new ProvenanceEntryDto(tableName, col.Name, col.Type, ProvenanceSource.Mocked, rowCount));
                }
            }

            tables.Add(new BlendedTable(tableName, blendedColumns, blendedRows));
        }

        return new BlendResult(tables, provenance);
    }

    private static IEnumerable<(string TableName, List<(string Name, string Type)> Columns)> EnumerateBlueprintTables(JsonElement blueprint)
    {
        if (!blueprint.TryGetProperty("data_model", out var dataModel))
            yield break;

        foreach (var section in new[] { "fact_tables", "dimension_tables" })
        {
            if (!dataModel.TryGetProperty(section, out var tables) || tables.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var table in tables.EnumerateArray())
            {
                var tableName = table.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(tableName)) continue;

                var columns = new List<(string Name, string Type)>();
                if (table.TryGetProperty("columns", out var cols) && cols.ValueKind == JsonValueKind.Array)
                {
                    foreach (var col in cols.EnumerateArray())
                    {
                        var colName = col.TryGetProperty("name", out var cn) ? cn.GetString() ?? "" : "";
                        var colType = col.TryGetProperty("type", out var ct) ? ct.GetString() ?? "VARCHAR" : "VARCHAR";
                        if (!string.IsNullOrWhiteSpace(colName))
                            columns.Add((colName, colType));
                    }
                }

                yield return (tableName, columns);
            }
        }
    }
}
