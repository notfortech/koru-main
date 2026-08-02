using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using StudioTechBI.Application.DTOs.InsightsEngine;
using StudioTechBI.Application.DTOs.PowerBI;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;

namespace StudioTechBI.Application.Services;

/// <inheritdoc />
public sealed class PowerBiDatasetSampleService : IPowerBiDatasetSampleService
{
    private static readonly Regex BareQuotedTable = new("^'(?:''|[^'])*'$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly PowerBISettings _power;
    private readonly IOptionsMonitor<ReportInsightsOptions> _opts;
    private readonly ILogger<PowerBiDatasetSampleService> _logger;

    public PowerBiDatasetSampleService(
        IOptions<PowerBISettings> powerBiSettings,
        IOptionsMonitor<ReportInsightsOptions> opts,
        ILogger<PowerBiDatasetSampleService> logger)
    {
        _power = powerBiSettings.Value;
        _opts = opts;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DatasetQueryTableSample>> GetDatasetSamplesAsync(
        PowerBiAssetDto? asset,
        string reportType,
        string activePageName,
        string? impersonatedUserPrincipalName,
        CancellationToken cancellationToken = default)
    {
        var opt = _opts.CurrentValue;
        if (!opt.DatasetExecuteQueriesEnabled)
            return Array.Empty<DatasetQueryTableSample>();

        var ws = (asset?.WorkspaceId ?? "").Trim();
        var ds = (asset?.DatasetId ?? "").Trim();
        if (ws.Length == 0 || ds.Length == 0)
        {
            _logger.LogInformation("Dataset samples skipped: missing WorkspaceId or DatasetId on asset.");
            return Array.Empty<DatasetQueryTableSample>();
        }

        var queries = ResolveQueries(opt, (reportType ?? "").Trim(), (activePageName ?? "").Trim());
        if (queries.Count == 0 && opt.AutoDiscoverTables)
        {
            queries = await BuildDiscoveryQueriesAsync(ws, ds, impersonatedUserPrincipalName, opt, cancellationToken)
                .ConfigureAwait(false);
        }

        if (queries.Count == 0)
            return Array.Empty<DatasetQueryTableSample>();

        string token;
        try
        {
            token = await AcquirePowerBiTokenAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Acquire Power BI token failed; skipping dataset samples.");
            return Array.Empty<DatasetQueryTableSample>();
        }

        var samples = new List<DatasetQueryTableSample>();
        var rowsUsed = 0;
        using var http = new HttpClient();
        foreach (var (label, query) in queries)
        {
            if (rowsUsed >= opt.MaxTotalSampleRows)
                break;

            var remainingBudget = Math.Max(1, opt.MaxTotalSampleRows - rowsUsed);
            var cappedRows = Math.Min(opt.MaxRowsPerQuery, remainingBudget);

            var dax = ApplyRowCap(query.Trim(), cappedRows);
            try
            {
                var raw = await ExecuteQueryRawAsync(http, token, ws, ds, dax, impersonatedUserPrincipalName, cancellationToken)
                    .ConfigureAwait(false);
                var table = ParseSingleTable(raw, $"dax:{label}");
                if (table != null && table.Rows.Count > 0)
                {
                    samples.Add(table);
                    rowsUsed += table.Rows.Count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "executeQueries failed for label {Label}.", label);
            }
        }

        return samples;
    }

    private static List<(string Label, string Query)> ResolveQueries(
        ReportInsightsOptions opt,
        string reportType,
        string activePage)
    {
        var list = new List<(string Label, string Query)>();
        foreach (var profile in opt.Profiles)
        {
            if (!string.IsNullOrWhiteSpace(profile.ReportType)
                && !string.Equals(profile.ReportType.Trim(), reportType, StringComparison.OrdinalIgnoreCase))
                continue;

            var sub = (profile.PageNameContains ?? "").Trim();
            if (sub.Length > 0
                && activePage.IndexOf(sub, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            foreach (var q in profile.Queries)
            {
                var label = (q.Label ?? "").Trim();
                var query = (q.Query ?? "").Trim();
                if (label.Length == 0 || query.Length == 0 || !query.StartsWith("EVAL", StringComparison.OrdinalIgnoreCase))
                    continue;
                list.Add((label, query));
            }
        }

        return list;
    }

    /// <summary>Prefer replacing the first TOPN arity; otherwise wrap bare quoted table refs as TOPN(cap, …).</summary>
    private static string ApplyRowCap(string dax, int cap)
    {
        if (cap <= 0)
            return dax;
        var idx = dax.IndexOf("TOPN(", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var start = idx + "TOPN(".Length;
            var end = FindTopNComma(dax, start);
            if (end < 0)
                return dax;
            return dax[..start] + cap + dax[end..];
        }

        var inner = dax.StartsWith("EVALUATE ", StringComparison.OrdinalIgnoreCase)
            ? dax["EVALUATE ".Length..].TrimStart()
            : dax.Trim();
        if (IsBareQuotedTableReference(inner))
            return $"EVALUATE TOPN({cap}, {inner})";

        return dax;
    }

    private static bool IsBareQuotedTableReference(string expr) => BareQuotedTable.IsMatch(expr.Trim());

    /// <summary>Find comma that separates TOPN arity from table expression.</summary>
    private static int FindTopNComma(string dax, int from)
    {
        var depth = 0;
        for (var i = from; i < dax.Length; i++)
        {
            switch (dax[i])
            {
                case '(': depth++; break;
                case ')': depth = Math.Max(0, depth - 1); break;
                case ',' when depth == 0:
                    return i;
            }
        }

        return -1;
    }

    private async Task<List<(string Label, string Query)>> BuildDiscoveryQueriesAsync(
        string workspaceId,
        string datasetId,
        string? impersonatedUserPrincipalName,
        ReportInsightsOptions opt,
        CancellationToken ct)
    {
        var queries = new List<(string Label, string Query)>();
        string token;
        try
        {
            token = await AcquirePowerBiTokenAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            return queries;
        }

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var url = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/datasets/{datasetId}/tables";
        using var listResp = await http.GetAsync(url, ct).ConfigureAwait(false);
        var listBody = await listResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!listResp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Power BI List tables failed: {Code} {Body}", (int)listResp.StatusCode, Truncate(listBody, 500));
            return queries;
        }

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(listBody) ? "{}" : listBody);
        if (!doc.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
            return queries;

        var taken = 0;
        foreach (var t in value.EnumerateArray())
        {
            if (t.ValueKind != JsonValueKind.Object)
                continue;

            var hidden = t.TryGetProperty("isHidden", out var ih) && ih.ValueKind == JsonValueKind.True;
            if (hidden)
                continue;

            var name = t.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var nameTrim = name.Trim();
            if (opt.ExcludedTableNameFragments.Any(fragment =>
                    !string.IsNullOrWhiteSpace(fragment)
                    && nameTrim.Contains(fragment.Trim(), StringComparison.OrdinalIgnoreCase)))
                continue;

            var quoted = "'" + nameTrim.Replace("'", "''", StringComparison.Ordinal) + "'";
            var q = $"EVALUATE TOPN({opt.MaxRowsPerQuery}, {quoted})";
            queries.Add(($"table:{nameTrim}", q));
            taken++;
            if (taken >= opt.MaxTablesToSample)
                break;
        }

        return queries;
    }

    private async Task<string> ExecuteQueryRawAsync(
        HttpClient http,
        string token,
        string workspaceId,
        string datasetId,
        string dax,
        string? impersonatedUserPrincipalName,
        CancellationToken ct)
    {
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var url = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/datasets/{datasetId}/executeQueries";
        var payload = new Dictionary<string, object?>
        {
            ["queries"] = new object[] { new Dictionary<string, string> { ["query"] = dax } },
            ["serializerSettings"] = new Dictionary<string, bool> { ["includeNulls"] = false },
        };
        if (!string.IsNullOrWhiteSpace(impersonatedUserPrincipalName))
            payload["impersonatedUserName"] = impersonatedUserPrincipalName.Trim();

        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        using var resp = await http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"), ct)
            .ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"executeQueries {(int)resp.StatusCode}: {Truncate(body, 600)}");

        return body;
    }

    private static DatasetQueryTableSample? ParseSingleTable(string jsonBody, string sourceLabel)
    {
        using var doc = JsonDocument.Parse(jsonBody);
        var root = doc.RootElement;
        if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
            return null;
        var r0 = results[0];
        if (!r0.TryGetProperty("tables", out var tables) || tables.ValueKind != JsonValueKind.Array || tables.GetArrayLength() == 0)
            return null;
        var t0 = tables[0];

        var columns = new List<string>();
        if (t0.TryGetProperty("columns", out var colsEl) && colsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in colsEl.EnumerateArray())
            {
                if (c.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.String)
                {
                    var name = nm.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                        columns.Add(StripBracketName(name.Trim()));
                }
            }
        }

        var rowsOut = new List<Dictionary<string, string>>();
        if (!t0.TryGetProperty("rows", out var rowsEl) || rowsEl.ValueKind != JsonValueKind.Array)
            goto done;

        foreach (var row in rowsEl.EnumerateArray())
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (row.ValueKind == JsonValueKind.Array && columns.Count > 0)
            {
                var i = 0;
                foreach (var cell in row.EnumerateArray())
                {
                    var colKey = i < columns.Count ? columns[i] : $"col{i}";
                    dict[colKey] = CellToString(colKey, cell);
                    i++;
                }
            }
            else if (row.ValueKind == JsonValueKind.Array && columns.Count == 0)
            {
                // Position-only
                var j = 0;
                foreach (var cell in row.EnumerateArray())
                {
                    var colKey = $"col{j}";
                    dict[colKey] = CellToString(colKey, cell);
                    j++;
                }
            }
            else if (row.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in row.EnumerateObject())
                {
                    var colKey = StripBracketName(p.Name);
                    dict[colKey] = PropToString(colKey, p.Value);
                }
            }

            if (dict.Count > 0)
                rowsOut.Add(dict);
        }

    done:
        if (columns.Count == 0 && rowsOut.Count > 0)
        {
            var keys = rowsOut[0].Keys.OrderBy(k => k).ToList();
            foreach (var k in keys)
            {
                if (!columns.Contains(k, StringComparer.OrdinalIgnoreCase))
                    columns.Add(k);
            }
        }

        return rowsOut.Count == 0 ? null : new DatasetQueryTableSample { Source = sourceLabel, Columns = columns, Rows = rowsOut };
    }

    private static string CellToString(string columnName, JsonElement cell)
    {
        if (cell.ValueKind == JsonValueKind.Array && cell.GetArrayLength() > 0)
            return PropToString(columnName, cell[0]);
        return PropToString(columnName, cell);
    }

    private static string PropToString(string columnName, JsonElement cell)
    {
        if (cell.ValueKind == JsonValueKind.Object)
        {
            // Often { "[Col]": { "prop": ... } or value shape
            if (cell.TryGetProperty("value", out var inner))
                return ElementToString(columnName, inner);

            foreach (var p in cell.EnumerateObject())
            {
                var v = p.Value;
                if (v.ValueKind == JsonValueKind.Object && v.TryGetProperty("value", out var vv))
                    return ElementToString(columnName, vv);
                if (v.ValueKind == JsonValueKind.String || v.ValueKind == JsonValueKind.Number || v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
                    return ElementToString(columnName, v);
            }

            return cell.ToString();
        }

        return ElementToString(columnName, cell);
    }

    // Plausible OLE Automation date serial range: 1 (1899-12-31) to 60000 (~2064-06-03).
    private const double MinExcelSerialDate = 1d;
    private const double MaxExcelSerialDate = 60000d;

    private static bool LooksLikeDateColumn(string columnName) =>
        columnName.Contains("date", StringComparison.OrdinalIgnoreCase) ||
        columnName.Contains("time", StringComparison.OrdinalIgnoreCase);

    /// <summary>Power BI's executeQueries can return date/datetime columns as raw OLE Automation
    /// serial numbers rather than ISO strings. Detect that by column name + a plausible serial
    /// range and format as a real date, instead of letting the bare serial leak into
    /// AI-generated insights (which cites every DatasetSamples number as fact).</summary>
    private static string ElementToString(string columnName, JsonElement cell)
    {
        if (cell.ValueKind == JsonValueKind.String && cell.GetString() is { } s)
            return s;
        if (cell.ValueKind == JsonValueKind.Null || cell.ValueKind == JsonValueKind.Undefined)
            return "";

        if (cell.ValueKind == JsonValueKind.Number
            && LooksLikeDateColumn(columnName)
            && cell.TryGetDouble(out var num)
            && num >= MinExcelSerialDate && num <= MaxExcelSerialDate)
        {
            try
            {
                var dt = DateTime.FromOADate(num);
                return dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("yyyy-MM-dd") : dt.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch (ArgumentException)
            {
                // Not actually a valid OADate serial; fall through to the raw number below.
            }
        }

        return cell.ToString();
    }

    private static string StripBracketName(string name)
    {
        var s = name.Trim();
        var i = s.LastIndexOf('[');
        var j = s.LastIndexOf(']');
        if (i >= 0 && j > i)
            return s[(i + 1)..j].Trim();

        var dot = s.LastIndexOf('.');
        return dot >= 0 && dot < s.Length - 1 ? s[(dot + 1)..].Trim() : s;
    }

    private async Task<string> AcquirePowerBiTokenAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_power.ClientId)
            || string.IsNullOrWhiteSpace(_power.ClientSecret)
            || string.IsNullOrWhiteSpace(_power.TenantId))
        {
            throw new InvalidOperationException("Power BI service principal settings (TenantId, ClientId, ClientSecret) are not configured.");
        }

        var app = ConfidentialClientApplicationBuilder
            .Create(_power.ClientId)
            .WithClientSecret(_power.ClientSecret)
            .WithAuthority($"https://login.microsoftonline.com/{_power.TenantId}")
            .Build();

        var result = await app.AcquireTokenForClient(new[] { "https://analysis.windows.net/powerbi/api/.default" })
            .ExecuteAsync(ct)
            .ConfigureAwait(false);
        return result.AccessToken;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
