using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace StudioTechBI.Infrastructure.ValueConversion;

/// <summary>
/// Converts between a string property and a SQL uniqueidentifier.
/// Tolerates environments where the column is uniqueidentifier, while the app model uses string for API/UI.
/// </summary>
internal static class GuidStringConverters
{
    public static ValueConverter<string?, Guid?> NullableStringToGuid()
    {
        Expression<Func<string?, Guid?>> toProvider = v =>
            v == null || string.IsNullOrWhiteSpace(v) ? (Guid?)null : Guid.Parse(v);
        Expression<Func<Guid?, string?>> fromProvider = v => v.HasValue ? v.Value.ToString() : null;
        return new ValueConverter<string?, Guid?>(toProvider, fromProvider, convertsNulls: true);
    }
}
