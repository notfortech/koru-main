using ExcelDataReader;
using StudioTechBI.Application.DTOs.ReportDesigner;

namespace StudioTechBI.Application.Utilities;

/// <summary>
/// Extracts structural schema metadata (table name, column names, inferred types, row count)
/// from Excel/CSV files. Never returns actual cell values — schema metadata only.
/// </summary>
public static class ExcelSchemaExtractor
{
    private const int TypeInferenceSampleRows = 50;
    private const int MaxBufferedBytes = 50 * 1024 * 1024;

    /// <summary>
    /// Returns a <see cref="TableSchemaDto"/> for the first worksheet. Reads up to
    /// <see cref="TypeInferenceSampleRows"/> data rows to infer column types — those row
    /// values are discarded immediately and never returned to callers.
    /// </summary>
    public static async Task<TableSchemaDto> ExtractAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        await using var buffered = !stream.CanSeek
            ? await BufferAsync(stream, MaxBufferedBytes, cancellationToken)
            : null;

        if (stream.CanSeek) stream.Position = 0;
        var source = buffered ?? stream;

        using var reader = ExcelReaderFactory.CreateReader(source);

        if (!reader.Read())
            throw new InvalidOperationException("Excel file is empty or has no header row.");

        // Read header row — these are column names, not data values
        var headers = new List<string>();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            headers.Add(reader.GetValue(i)?.ToString()?.Trim() ?? $"Column{i + 1}");
        }

        headers = NormalizeHeaders(headers);
        if (headers.Count == 0)
            throw new InvalidOperationException("No column headers found in the Excel file.");

        // Infer types from up to TypeInferenceSampleRows rows — values are used only for
        // type detection and are immediately discarded after the loop.
        var typeSamples = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in headers) typeSamples[h] = new List<string>();

        var rowCount = 0;
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowCount++;

            if (rowCount <= TypeInferenceSampleRows)
            {
                for (var i = 0; i < headers.Count; i++)
                {
                    var v = i < reader.FieldCount ? reader.GetValue(i)?.ToString() ?? "" : "";
                    typeSamples[headers[i]].Add(v);
                }
            }
        }

        // Clear sample buffers now — values no longer needed
        var columns = headers.Select(h =>
        {
            var samples = typeSamples[h];
            var dataType = InferType(samples);
            typeSamples[h] = null!; // allow GC
            return new ColumnSchemaDto(h, dataType, IsNullable: true, MaxLength: null);
        }).ToList();

        var sheetName = Path.GetFileNameWithoutExtension(fileName);

        return new TableSchemaDto(
            TableName: sheetName,
            SheetName: sheetName,
            RowCount: rowCount,
            Columns: columns);
    }

    private static string InferType(List<string> samples)
    {
        var nonEmpty = samples.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (nonEmpty.Count == 0) return "string";

        if (nonEmpty.All(s => long.TryParse(s, out _))) return "integer";
        if (nonEmpty.All(s => decimal.TryParse(s, out _))) return "decimal";
        if (nonEmpty.All(s => DateTime.TryParse(s, out _))) return "datetime";
        if (nonEmpty.All(s => bool.TryParse(s, out _))) return "boolean";
        return "string";
    }

    private static List<string> NormalizeHeaders(List<string> raw)
    {
        var result = new List<string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < raw.Count; i++)
        {
            var name = string.IsNullOrWhiteSpace(raw[i]) ? $"Column{i + 1}" : raw[i];
            var candidate = name;
            var suffix = 2;
            while (!used.Add(candidate)) candidate = $"{name}_{suffix++}";
            result.Add(candidate);
        }
        return result;
    }

    private static async Task<MemoryStream> BufferAsync(Stream input, int maxBytes, CancellationToken ct)
    {
        var ms = new MemoryStream();
        var buf = new byte[81920];
        var total = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var read = await input.ReadAsync(buf.AsMemory(0, buf.Length), ct);
            if (read <= 0) break;
            total += read;
            if (total > maxBytes)
                throw new NotSupportedException($"File exceeds the {maxBytes / (1024 * 1024)} MB schema extraction limit.");
            await ms.WriteAsync(buf.AsMemory(0, read), ct);
        }
        ms.Position = 0;
        return ms;
    }
}
