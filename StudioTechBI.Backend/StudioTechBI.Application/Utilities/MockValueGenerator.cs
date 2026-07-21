namespace StudioTechBI.Application.Utilities;

/// <summary>
/// Deterministic, schema-driven mock value generation for the Dashboard Template Generator's
/// blend step. Mirrors the existing DashboardGenerator/mockReport.js approach (derive mock
/// shape from a blueprint's declared column types, seeded for reproducibility) rather than
/// DashboardGenerator/generate_mock_data.py's per-industry hardcoded pools — this needs to work
/// for any blueprint, not one fixed industry.
/// Column types follow the Analytics Blueprint Schema's data_model vocabulary
/// (INT | DECIMAL | VARCHAR | DATE | BIT), not ExcelSchemaExtractor's inferred-type vocabulary.
/// </summary>
public static class MockValueGenerator
{
    public static List<string> Generate(string tableName, string columnName, string declaredType, int rowCount)
    {
        var seed = StableSeed(tableName, columnName);
        var rnd = new Random(seed);
        var type = (declaredType ?? "").Trim().ToUpperInvariant();

        var values = new List<string>(rowCount);
        for (var i = 0; i < rowCount; i++)
        {
            values.Add(type switch
            {
                "INT" => rnd.Next(1, 10_000).ToString(),
                "DECIMAL" or "MONEY" or "FLOAT" => Math.Round(rnd.NextDouble() * 10_000, 2).ToString("F2"),
                "DATE" or "DATETIME" => DateTime.UtcNow.AddDays(-rnd.Next(0, 730)).ToString("yyyy-MM-dd"),
                "BIT" or "BOOLEAN" or "BOOL" => (rnd.Next(0, 2) == 1).ToString(),
                _ => $"{columnName}-{rnd.Next(1000, 9999)}",
            });
        }
        return values;
    }

    /// <summary>Stable across process restarts (unlike GetHashCode), so the same blueprint
    /// produces the same mock values on regeneration.</summary>
    private static int StableSeed(string tableName, string columnName)
    {
        unchecked
        {
            var hash = 17;
            foreach (var c in $"{tableName}|{columnName}")
                hash = hash * 31 + c;
            return hash & 0x7FFFFFFF;
        }
    }
}
