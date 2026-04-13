using System.Security.Cryptography;
using System.Text;

namespace StudioTechBI.Application.Utilities;

public static class SchemaHashHelper
{
    /// <summary>Deterministic hash of ordered column names (schema fingerprint).</summary>
    public static string ComputeSchemaHash(IReadOnlyList<string> columns)
    {
        if (columns == null || columns.Count == 0)
            throw new ArgumentException("At least one column is required for schema hash.", nameof(columns));

        var joined = string.Join("|", columns.Select(c => (c ?? "").Trim()));
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(joined));
        return Convert.ToBase64String(bytes);
    }
}
