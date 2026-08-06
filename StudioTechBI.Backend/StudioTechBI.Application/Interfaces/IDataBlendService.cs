using System.Text.Json;
using StudioTechBI.Application.DTOs.DashboardTemplate;

namespace StudioTechBI.Application.Interfaces;

public record BlendedTable(string TableName, List<string> Columns, List<Dictionary<string, string>> Rows);

public record BlendResult(List<BlendedTable> Tables, List<ProvenanceEntryDto> Provenance);

/// <summary>
/// Deterministic (no AI/LLM call) blend of a client's uploaded file with mock data, driven by
/// a blueprint's data_model. Real cell values are read via the existing ExcelSampleExtractor —
/// the same safe path DataSamplingService already uses — and never leave this service; they are
/// not sent to any AI call, preserving the existing "AI never sees data values" boundary.
/// </summary>
public interface IDataBlendService
{
    Task<BlendResult> BlendAsync(Stream uploadedFile, JsonElement blueprint, CancellationToken cancellationToken = default);

    /// <summary>Same real-value-or-mock blend as <see cref="BlendAsync"/>, but driven by an
    /// already-resolved flat column list (name + declared type) for a single table, instead of a
    /// blueprint's data_model JSON. Used by the AI-assisted "closest HTML template" fallback,
    /// whose schema declaration (an HTML template manifest's requiredColumns/optionalColumns) has
    /// no blueprint shape to enumerate.</summary>
    Task<BlendResult> BlendFromColumnListAsync(
        Stream uploadedFile,
        string tableName,
        IReadOnlyList<(string Name, string Type)> columns,
        CancellationToken cancellationToken = default);
}
