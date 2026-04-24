using ExcelDataReader;

namespace StudioTechBI.Application.Utilities;

/// <summary>Reads Excel header + up to <paramref name="maxDataRows"/> rows for preview.</summary>
public static class ExcelSampleExtractor
{
    public const int DefaultMaxRows = 100;
    private const int MaxBufferedBytes = 25 * 1024 * 1024; // 25 MB safety cap for non-seekable XLSX streams

    public static async Task<(List<string> Columns, List<Dictionary<string, string>> Rows)> ExtractAsync(
        Stream stream,
        int maxDataRows = DefaultMaxRows,
        CancellationToken cancellationToken = default)
    {
        if (maxDataRows < 1)
            maxDataRows = DefaultMaxRows;

        // XLSX is a zip container and many readers require a seekable stream.
        // Azure blob download streams are often non-seekable, so we buffer with a cap.
        await using var buffered = !stream.CanSeek
            ? await BufferToMemoryAsync(stream, MaxBufferedBytes, cancellationToken)
            : null;

        if (stream.CanSeek)
            stream.Position = 0;

        // ExcelDataReader reads XLS/XLSX from a stream.
        using var reader = ExcelReaderFactory.CreateReader(buffered ?? stream);

        if (!reader.Read())
            throw new InvalidOperationException("Excel is empty.");

        var rawHeaders = new List<string>();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var h = reader.GetValue(i)?.ToString()?.Trim() ?? "";
            rawHeaders.Add(h);
        }

        var columns = NormalizeHeaders(rawHeaders);
        if (columns.Count == 0)
            throw new InvalidOperationException("Excel header contains no column names.");

        var rows = new List<Dictionary<string, string>>();
        var count = 0;
        while (count < maxDataRows && reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                var v = i < reader.FieldCount ? reader.GetValue(i) : null;
                row[col] = v?.ToString() ?? "";
            }

            rows.Add(row);
            count++;
        }

        return (columns, rows);
    }

    private static List<string> NormalizeHeaders(List<string> raw)
    {
        var result = new List<string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < raw.Count; i++)
        {
            var baseName = raw[i];
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = $"Column{i + 1}";

            var name = baseName;
            var suffix = 2;
            while (!used.Add(name))
            {
                name = $"{baseName}_{suffix}";
                suffix++;
            }

            result.Add(name);
        }
        return result;
    }

    private static async Task<MemoryStream> BufferToMemoryAsync(Stream input, int maxBytes, CancellationToken ct)
    {
        var ms = new MemoryStream();
        var buffer = new byte[81920];
        var total = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read <= 0) break;
            total += read;
            if (total > maxBytes)
                throw new NotSupportedException($"Excel file is too large to sample safely (> {maxBytes / (1024 * 1024)} MB).");
            await ms.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        ms.Position = 0;
        return ms;
    }
}

