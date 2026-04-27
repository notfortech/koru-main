using System.Text.Json;

namespace StudioTechBI.Application.Utilities;

/// <summary>Extracts column names from JSON sample payloads used by InsightsEngine copilot routes.</summary>
public static class InsightsSampleColumnExtractor
{
    /// <summary>
    /// Expects a JSON array of row objects; uses the union of keys from the first few rows.
    /// </summary>
    public static IReadOnlyList<string> ExtractColumnNames(JsonElement sample)
    {
        if (sample.ValueKind != JsonValueKind.Array || sample.GetArrayLength() == 0)
            return Array.Empty<string>();

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var take = Math.Min((int)sample.GetArrayLength(), 20);
        for (var i = 0; i < take; i++)
        {
            var row = sample[i];
            if (row.ValueKind != JsonValueKind.Object)
                continue;
            foreach (var p in row.EnumerateObject())
            {
                if (!string.IsNullOrWhiteSpace(p.Name))
                    names.Add(p.Name.Trim());
            }
        }

        return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
