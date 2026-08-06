using System.Text.Json;

namespace StudioTechBI.Application.Utilities;

/// <summary>
/// Maps an HTML report template's manifest.json (requiredColumns/optionalColumns/dataContract)
/// into the flat (tableName, columns) shape IDataBlendService.BlendFromColumnListAsync expects.
/// Used by the AI-assisted "closest HTML template" fallback (ReportGeneratorController.GenerateAsync)
/// to blend a client's upload against the schema a role-gate-passed-but-not-confident candidate
/// declares, filling any gaps with mock data.
/// </summary>
public static class HtmlManifestColumnMapper
{
    public static (string TableName, List<(string Name, string Type)> Columns) ToBlendColumns(string manifestJson)
    {
        using var doc = JsonDocument.Parse(manifestJson);
        var root = doc.RootElement;

        var tableName = root.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "Data" : "Data";

        var roleByColumn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("dataContract", out var dataContract)
            && dataContract.TryGetProperty("rowFields", out var rowFields)
            && rowFields.ValueKind == JsonValueKind.Array)
        {
            foreach (var field in rowFields.EnumerateArray())
            {
                var column = field.TryGetProperty("column", out var c) ? c.GetString() : null;
                var role = field.TryGetProperty("role", out var r) ? r.GetString() : null;
                if (!string.IsNullOrWhiteSpace(column) && !string.IsNullOrWhiteSpace(role))
                    roleByColumn[column] = role;
            }
        }

        var allColumns = new List<string>();
        void AddAll(string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return;

            foreach (var v in arr.EnumerateArray())
            {
                var name = v.GetString();
                if (!string.IsNullOrWhiteSpace(name) && !allColumns.Contains(name, StringComparer.OrdinalIgnoreCase))
                    allColumns.Add(name);
            }
        }
        AddAll("requiredColumns");
        AddAll("optionalColumns");

        var columns = allColumns
            .Select(name => (Name: name, Type: ToDeclaredType(roleByColumn.GetValueOrDefault(name))))
            .ToList();

        return (tableName, columns);
    }

    private static string ToDeclaredType(string? role) => role?.ToLowerInvariant() switch
    {
        "numeric" => "DECIMAL",
        "date" => "DATE",
        _ => "VARCHAR",
    };
}
