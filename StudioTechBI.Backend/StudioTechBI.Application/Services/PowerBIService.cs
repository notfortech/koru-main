using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Application.DTOs.AI;
using StudioTechBI.Application.DTOs.PowerBI;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace StudioTechBI.Application.Services;

public class PowerBIService
{
    private readonly PowerBISettings _settings;
    private readonly IPowerBiAssetQuery _powerBiAssetQuery;
    private readonly IBlobStorageService _blobStorage;

    public PowerBIService(
        IOptions<PowerBISettings> settings,
        IPowerBiAssetQuery powerBiAssetQuery,
        IBlobStorageService blobStorage)
    {
        _settings = settings.Value;
        _powerBiAssetQuery = powerBiAssetQuery;
        _blobStorage = blobStorage;
    }

    public async Task<object> GetEmbedToken(string periodType, string? period, string? clientCode = null)
    {
        string reportId = _settings.ReportId;
        string workspaceId = _settings.WorkspaceId;
        string? datasetId = _settings.DatasetId;

        if (!string.IsNullOrWhiteSpace(clientCode))
        {
            var code = clientCode.Trim();

            // Enforce "only display when client master xlsx exists".
            // This prevents showing a previous client's report when the new client isn't configured or has no blob master.
            if (!await MasterExcelExistsAsync(code))
                throw new InvalidOperationException($"No master file found in blob for client '{code}'. Please upload/prepare the master first.");

            var asset = await _powerBiAssetQuery.GetActiveAssetByClientCodeAsync(code, periodType, default);
            if (asset == null || string.IsNullOrWhiteSpace(asset.ReportId) || string.IsNullOrWhiteSpace(asset.WorkspaceId))
                throw new InvalidOperationException($"No Power BI report configured for client '{code}' (reportType='{periodType}').");

            reportId = asset.ReportId.Trim();
            workspaceId = asset.WorkspaceId.Trim();
            datasetId = string.IsNullOrWhiteSpace(asset.DatasetId) ? datasetId : asset.DatasetId.Trim();
        }

        var token = await GetAccessToken();
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var embedUrl =
            $"https://app.powerbi.com/reportEmbed?reportId={reportId}&groupId={workspaceId}";

        var embedTokenUrl =
            $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/reports/{reportId}/GenerateToken";

    var tokenRequest =
        new StringContent(
            JsonSerializer.Serialize(new
            {
                accessLevel = "view"
            }),
            Encoding.UTF8,
            "application/json"
        );

    var embedResponse =
            await httpClient.PostAsync(embedTokenUrl, tokenRequest);

        var embedJson =
            await embedResponse.Content.ReadAsStringAsync();

        var embed =
            JsonSerializer.Deserialize<JsonElement>(embedJson);

        return new
        {
            accessToken = embed.GetProperty("token").GetString(),
            embedUrl = embedUrl,
            reportId = reportId,
            datasetId = datasetId,
            periodType = periodType,
            period = period
        };
    }

    private async Task<bool> MasterExcelExistsAsync(string clientCode)
    {
        // Python worker uses:
        // - client folder: AU-001 (normalizes AU001 -> AU-001)
        // - master filename: Accounting_Master_au001.xlsx
        // - path: {clientFolder}/accounting/validated/{masterFilename}
        var normalized = (clientCode ?? "").Trim();
        if (string.IsNullOrEmpty(normalized))
            return false;

        var folder = normalized;
        if (normalized.StartsWith("AU", StringComparison.OrdinalIgnoreCase) && normalized.Length == 5 && !normalized.Contains('-'))
            folder = $"AU-{normalized[^3..]}";

        var basePart = normalized.ToLowerInvariant().Replace("-", "");
        var masterFile = $"Accounting_Master_{basePart}.xlsx";
        var blobPath = $"{folder}/accounting/validated/{masterFile}";

        return await _blobStorage.BlobExistsAsync(blobPath);
    }

private async Task<string> GetAccessToken()
{
    var app =
        ConfidentialClientApplicationBuilder
        .Create(_settings.ClientId)
        .WithClientSecret(_settings.ClientSecret)
        .WithAuthority($"https://login.microsoftonline.com/{_settings.TenantId}")
        .Build();

    var result =
        await app.AcquireTokenForClient(
            new[] { "https://analysis.windows.net/powerbi/api/.default" })
        .ExecuteAsync();

    return result.AccessToken;
}

public async Task<object> GetReports()
{
    var token = await GetAccessToken();

    using var client = new HttpClient();

    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", token);

    var url =
        $"https://api.powerbi.com/v1.0/myorg/groups/{_settings.WorkspaceId}/reports";

    var response =
        await client.GetAsync(url);

    var json =
        await response.Content.ReadAsStringAsync();

    var result =
        JsonSerializer.Deserialize<JsonElement>(json);

    var reports =
        result.GetProperty("value")
        .EnumerateArray()
        .Select(r => new
        {
            id = r.GetProperty("id").GetString(),
            name = r.GetProperty("name").GetString()
        });

    return reports;
}

public async Task<DashboardSummaryDto> GetDashboardSummary(
    string periodType,
    string? period,
    string? clientCode)
{
    // 🚨 TEMP MOCK (replace later with real query / SP / warehouse)
    // This keeps your system working while you build real pipeline

    await Task.Delay(50); // simulate async

    return new DashboardSummaryDto
    {
        Period = period ?? "Current",
        Revenue = 50000,
        Expenses = 42000,
        Profit = 8000,
        GrowthPercentage = 12.5,
        TopRegion = "Victoria"
    };
}

}
