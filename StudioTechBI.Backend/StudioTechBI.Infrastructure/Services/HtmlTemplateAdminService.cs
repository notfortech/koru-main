using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.Constants;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Services;

/// <summary>
/// See IHtmlTemplateAdminService. Every write path (batch upload, single-manifest edit) shares one
/// validation routine (<see cref="ValidateManifestSchema"/>) so the two entry points can never
/// silently drift apart as the manifest schema grows, and never writes a manifest to the live blob
/// path unless it passes.
/// </summary>
public sealed class HtmlTemplateAdminService : IHtmlTemplateAdminService
{
    private static readonly Regex KebabCaseId = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions PrettyPrint = new() { WriteIndented = true };

    private readonly IBlobStorageService _blobStorage;
    private readonly IHtmlTemplateSyncRunner _syncRunner;
    private readonly ILogger<HtmlTemplateAdminService> _logger;

    public HtmlTemplateAdminService(
        IBlobStorageService blobStorage,
        IHtmlTemplateSyncRunner syncRunner,
        ILogger<HtmlTemplateAdminService> logger)
    {
        _blobStorage = blobStorage;
        _syncRunner = syncRunner;
        _logger = logger;
    }

    public async Task<HtmlTemplateBulkUploadResponseDto> UploadBatchAsync(Stream zipStream, CancellationToken cancellationToken = default)
    {
        var results = new List<HtmlTemplateUploadResultDto>();
        var existingIds = await LoadIndexAsync(cancellationToken);
        var mergedIds = new List<string>(existingIds);
        var anySucceeded = false;

        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        var manifestEntries = archive.Entries
            .Where(e => e.Length > 0 && string.Equals(e.Name, "manifest.json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (manifestEntries.Count == 0)
        {
            results.Add(new HtmlTemplateUploadResultDto(
                "(zip)", "Failed", new List<string> { "No manifest.json files were found anywhere in the uploaded zip." }, new List<string>()));
            return new HtmlTemplateBulkUploadResponseDto(results, 0, 0, 1);
        }

        foreach (var manifestEntry in manifestEntries)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            string? templateId = null;

            try
            {
                string manifestText;
                await using (var entryStream = manifestEntry.Open())
                using (var reader = new StreamReader(entryStream, Encoding.UTF8))
                {
                    manifestText = await reader.ReadToEndAsync(cancellationToken);
                }

                using var manifestDoc = JsonDocument.Parse(manifestText);
                var root = manifestDoc.RootElement;

                templateId = GetString(root, "id");

                var (schemaValid, schemaErrors, schemaWarnings) = ValidateManifestSchema(root);
                errors.AddRange(schemaErrors);
                warnings.AddRange(schemaWarnings);

                if (string.IsNullOrWhiteSpace(templateId))
                {
                    results.Add(new HtmlTemplateUploadResultDto(
                        manifestEntry.FullName, "Failed", errors.Count > 0 ? errors : new List<string> { "manifest.json has no valid 'id' field." }, warnings));
                    continue;
                }

                var directory = GetContainingDirectory(manifestEntry.FullName);
                var chromeEntry = FindSiblingEntry(archive, directory, "chrome.html");
                string? chromeHtml = null;

                if (chromeEntry is null)
                {
                    errors.Add("chrome.html not found alongside manifest.json.");
                }
                else
                {
                    await using var chromeStream = chromeEntry.Open();
                    using var chromeReader = new StreamReader(chromeStream, Encoding.UTF8);
                    chromeHtml = await chromeReader.ReadToEndAsync(cancellationToken);

                    var (chromeValid, chromeErrors, chromeWarnings) = ValidateChromeHtml(chromeHtml, root);
                    errors.AddRange(chromeErrors);
                    warnings.AddRange(chromeWarnings);
                }

                if (errors.Count > 0)
                {
                    results.Add(new HtmlTemplateUploadResultDto(templateId, "Failed", errors, warnings));
                    continue;
                }

                var isUpdate = existingIds.Contains(templateId, StringComparer.Ordinal);

                using (var manifestBytes = new MemoryStream(Encoding.UTF8.GetBytes(manifestText)))
                    await _blobStorage.UploadClientBlobAsync(HtmlTemplateBlobPaths.ManifestPath(templateId), manifestBytes, "application/json", cancellationToken);

                using (var chromeBytes = new MemoryStream(Encoding.UTF8.GetBytes(chromeHtml!)))
                    await _blobStorage.UploadClientBlobAsync(HtmlTemplateBlobPaths.ChromeHtmlPath(templateId), chromeBytes, "text/html", cancellationToken);

                var previewEntry = FindSiblingEntry(archive, directory, "preview.jpg");
                if (previewEntry is not null)
                {
                    await using var previewStream = previewEntry.Open();
                    using var previewBuffer = new MemoryStream();
                    await previewStream.CopyToAsync(previewBuffer, cancellationToken);
                    previewBuffer.Position = 0;
                    await _blobStorage.UploadClientBlobAsync(HtmlTemplateBlobPaths.PreviewPath(templateId), previewBuffer, "image/jpeg", cancellationToken);
                }

                if (!mergedIds.Contains(templateId, StringComparer.Ordinal))
                    mergedIds.Add(templateId);

                anySucceeded = true;
                results.Add(new HtmlTemplateUploadResultDto(templateId, isUpdate ? "Updated" : "Created", new List<string>(), warnings));
            }
            catch (JsonException jsonEx)
            {
                results.Add(new HtmlTemplateUploadResultDto(
                    templateId ?? manifestEntry.FullName, "Failed", new List<string> { $"manifest.json is not valid JSON: {jsonEx.Message}" }, new List<string>()));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HtmlTemplateAdmin.UploadBatch.TemplateFailed Entry={Entry}", manifestEntry.FullName);
                results.Add(new HtmlTemplateUploadResultDto(
                    templateId ?? manifestEntry.FullName, "Failed", new List<string> { $"Unexpected error: {ex.Message}" }, new List<string>()));
            }
        }

        if (anySucceeded)
        {
            try
            {
                await SaveIndexAsync(mergedIds, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HtmlTemplateAdmin.UploadBatch.IndexMergeFailed");
                results.Add(new HtmlTemplateUploadResultDto(
                    "index.json", "Failed",
                    new List<string> { $"Templates uploaded but the shared index.json failed to update: {ex.Message}. They will not be discoverable until this is retried." },
                    new List<string>()));
            }
        }

        var created = results.Count(r => r.Status == "Created");
        var updated = results.Count(r => r.Status == "Updated");
        var failed = results.Count(r => r.Status == "Failed");
        return new HtmlTemplateBulkUploadResponseDto(results, created, updated, failed);
    }

    public async Task<List<HtmlTemplateSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var ids = await LoadIndexAsync(cancellationToken);
        var result = new List<HtmlTemplateSummaryDto>();

        foreach (var id in ids)
        {
            await using var stream = await _blobStorage.DownloadBlobAsync(HtmlTemplateBlobPaths.ManifestPath(id), cancellationToken);
            if (stream is null)
            {
                result.Add(new HtmlTemplateSummaryDto(id, id, "", new List<string>(), new List<string>(), false, false, true, "manifest.json not found in blob."));
                continue;
            }

            try
            {
                using var manifestDoc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = manifestDoc.RootElement;
                var name = GetString(root, "name") ?? id;
                var industry = GetString(root, "industry") ?? "";
                var required = ReadStringArray(root, "requiredColumns");
                var optional = ReadStringArray(root, "optionalColumns");
                var hasThemeSlots = root.TryGetProperty("themeSlots", out var themeSlots) && themeSlots.ValueKind == JsonValueKind.Object;
                var hasPreview = await _blobStorage.BlobExistsAsync(HtmlTemplateBlobPaths.PreviewPath(id), cancellationToken);

                result.Add(new HtmlTemplateSummaryDto(id, name, industry, required, optional, hasPreview, hasThemeSlots, false, null));
            }
            catch (Exception ex)
            {
                result.Add(new HtmlTemplateSummaryDto(id, id, "", new List<string>(), new List<string>(), false, false, true, $"manifest.json failed to parse: {ex.Message}"));
            }
        }

        return result;
    }

    public async Task DeleteAsync(string templateId, CancellationToken cancellationToken = default)
    {
        var ids = await LoadIndexAsync(cancellationToken);
        var updated = ids.Where(id => !string.Equals(id, templateId, StringComparison.Ordinal)).ToList();
        if (updated.Count != ids.Count)
            await SaveIndexAsync(updated, cancellationToken);
    }

    public async Task<int> ForceSyncNowAsync(CancellationToken cancellationToken = default)
    {
        var result = await _syncRunner.RunOnceAsync(cancellationToken);
        return result.Total;
    }

    public async Task<string?> GetManifestRawAsync(string templateId, CancellationToken cancellationToken = default)
    {
        await using var stream = await _blobStorage.DownloadBlobAsync(HtmlTemplateBlobPaths.ManifestPath(templateId), cancellationToken);
        if (stream is null) return null;

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var raw = await reader.ReadToEndAsync(cancellationToken);

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return JsonSerializer.Serialize(doc.RootElement, PrettyPrint);
        }
        catch (JsonException)
        {
            // Live content should always be valid (writes are validated before landing), but if it
            // somehow isn't, still surface the raw text rather than a 500 -- the editor can show it.
            return raw;
        }
    }

    public async Task<HtmlTemplateUploadResultDto> UpdateManifestAsync(string templateId, string manifestJson, CancellationToken cancellationToken = default)
    {
        JsonDocument manifestDoc;
        try
        {
            manifestDoc = JsonDocument.Parse(manifestJson);
        }
        catch (JsonException jsonEx)
        {
            return new HtmlTemplateUploadResultDto(templateId, "Failed", new List<string> { $"Not valid JSON: {jsonEx.Message}" }, new List<string>());
        }

        using (manifestDoc)
        {
            var root = manifestDoc.RootElement;
            var parsedId = GetString(root, "id");
            if (!string.Equals(parsedId, templateId, StringComparison.Ordinal))
            {
                return new HtmlTemplateUploadResultDto(
                    templateId, "Failed",
                    new List<string> { $"manifest 'id' ('{parsedId}') does not match the template being edited ('{templateId}'). Renaming a template's id isn't supported here -- delete and re-upload under the new id instead." },
                    new List<string>());
            }

            var (isValid, errors, warnings) = ValidateManifestSchema(root);
            if (!isValid)
                return new HtmlTemplateUploadResultDto(templateId, "Failed", errors, warnings);

            using var manifestBytes = new MemoryStream(Encoding.UTF8.GetBytes(manifestJson));
            await _blobStorage.UploadClientBlobAsync(HtmlTemplateBlobPaths.ManifestPath(templateId), manifestBytes, "application/json", cancellationToken);

            try
            {
                await _syncRunner.RunOnceAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HtmlTemplateAdmin.UpdateManifest.SyncFailed TemplateId={TemplateId}", templateId);
            }

            return new HtmlTemplateUploadResultDto(templateId, "Updated", new List<string>(), warnings);
        }
    }

    // ── Validation ───────────────────────────────────────────────────────────────────────────

    private static (bool IsValid, List<string> Errors, List<string> Warnings) ValidateManifestSchema(JsonElement manifest)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var id = GetString(manifest, "id");
        if (string.IsNullOrWhiteSpace(id))
            errors.Add("Missing or empty 'id'.");
        else if (!KebabCaseId.IsMatch(id))
            errors.Add($"'id' must be kebab-case (lowercase letters, digits, hyphens only): '{id}'.");

        if (string.IsNullOrWhiteSpace(GetString(manifest, "name")))
            errors.Add("Missing or empty 'name'.");
        if (string.IsNullOrWhiteSpace(GetString(manifest, "industry")))
            errors.Add("Missing or empty 'industry'.");

        if (!manifest.TryGetProperty("requires", out var requires) || requires.ValueKind != JsonValueKind.Object)
        {
            errors.Add("Missing 'requires' object.");
        }
        else
        {
            foreach (var field in new[] { "minNumeric", "minDate", "minCategorical" })
            {
                if (!requires.TryGetProperty(field, out var value) || !value.TryGetInt32(out var intValue) || intValue < 0)
                    errors.Add($"'requires.{field}' must be a non-negative integer.");
            }
        }

        if (!manifest.TryGetProperty("requiredColumns", out var requiredColumns) || requiredColumns.ValueKind != JsonValueKind.Array)
            errors.Add("Missing 'requiredColumns' array.");
        else if (requiredColumns.GetArrayLength() == 0)
            warnings.Add("'requiredColumns' is empty -- matching will score 0 against this template.");

        if (!manifest.TryGetProperty("optionalColumns", out var optionalColumns) || optionalColumns.ValueKind != JsonValueKind.Array)
            errors.Add("Missing 'optionalColumns' array.");

        if (!manifest.TryGetProperty("dataContract", out var dataContract) || dataContract.ValueKind != JsonValueKind.Object)
        {
            errors.Add("Missing 'dataContract' object.");
        }
        else
        {
            if (!dataContract.TryGetProperty("maxRows", out var maxRows) || !maxRows.TryGetInt32(out var maxRowsValue) || maxRowsValue <= 0)
                errors.Add("'dataContract.maxRows' must be a positive integer.");
            if (!dataContract.TryGetProperty("rowFields", out var rowFields) || rowFields.ValueKind != JsonValueKind.Array)
                errors.Add("Missing 'dataContract.rowFields' array.");
        }

        if (!manifest.TryGetProperty("testIds", out var testIds) || testIds.ValueKind != JsonValueKind.Object)
        {
            errors.Add("Missing 'testIds' object -- template would be excluded from matching.");
        }
        else
        {
            foreach (var field in new[] { "resultsLoaded", "kpiPrefix", "chartPrefix" })
            {
                if (string.IsNullOrWhiteSpace(GetString(testIds, field)))
                    errors.Add($"'testIds.{field}' is missing or empty.");
            }
        }

        return (errors.Count == 0, errors, warnings);
    }

    private static (bool IsValid, List<string> Errors, List<string> Warnings) ValidateChromeHtml(string chromeHtml, JsonElement manifest)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (!chromeHtml.Contains("stbi-report-data", StringComparison.OrdinalIgnoreCase))
            errors.Add("chrome.html does not reference the 'stbi-report-data' marker -- it will not receive injected report data.");

        string? resultsLoadedId = null, kpiPrefix = null, chartPrefix = null;
        if (manifest.TryGetProperty("testIds", out var testIds) && testIds.ValueKind == JsonValueKind.Object)
        {
            resultsLoadedId = GetString(testIds, "resultsLoaded");
            kpiPrefix = GetString(testIds, "kpiPrefix");
            chartPrefix = GetString(testIds, "chartPrefix");
        }

        if (!string.IsNullOrWhiteSpace(resultsLoadedId) && !chromeHtml.Contains($"data-testid=\"{resultsLoadedId}\"", StringComparison.OrdinalIgnoreCase))
            errors.Add($"chrome.html has no element with data-testid=\"{resultsLoadedId}\" (testIds.resultsLoaded).");

        if (!string.IsNullOrWhiteSpace(kpiPrefix) && !chromeHtml.Contains($"data-testid=\"kpi-tile-{kpiPrefix}", StringComparison.OrdinalIgnoreCase))
            warnings.Add($"No 'data-testid=\"kpi-tile-{kpiPrefix}...\"' elements found -- confirm this template intentionally has no KPI tiles.");

        if (!string.IsNullOrWhiteSpace(chartPrefix) && !chromeHtml.Contains($"data-testid=\"chart-{chartPrefix}", StringComparison.OrdinalIgnoreCase))
            warnings.Add($"No 'data-testid=\"chart-{chartPrefix}...\"' elements found -- confirm this template intentionally has no charts.");

        return (errors.Count == 0, errors, warnings);
    }

    // ── index.json helpers ──────────────────────────────────────────────────────────────────────

    private async Task<List<string>> LoadIndexAsync(CancellationToken cancellationToken)
    {
        await using var stream = await _blobStorage.DownloadBlobAsync(HtmlTemplateBlobPaths.IndexPath, cancellationToken);
        if (stream is null) return new List<string>();

        var ids = await JsonSerializer.DeserializeAsync<List<string>>(stream, cancellationToken: cancellationToken);
        return ids ?? new List<string>();
    }

    private async Task SaveIndexAsync(List<string> ids, CancellationToken cancellationToken)
    {
        var deduped = ids.Distinct(StringComparer.Ordinal).ToList();
        var json = JsonSerializer.Serialize(deduped, PrettyPrint);
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await _blobStorage.UploadClientBlobAsync(HtmlTemplateBlobPaths.IndexPath, bytes, "application/json", cancellationToken);
    }

    // ── Zip helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Returns the entry's containing "directory" (with trailing slash), or "" if it's at the zip root.</summary>
    private static string GetContainingDirectory(string entryFullName)
    {
        var lastSlash = entryFullName.LastIndexOf('/');
        return lastSlash < 0 ? "" : entryFullName[..(lastSlash + 1)];
    }

    private static ZipArchiveEntry? FindSiblingEntry(ZipArchive archive, string directory, string fileName)
    {
        var target = directory + fileName;
        return archive.Entries.FirstOrDefault(e => string.Equals(e.FullName, target, StringComparison.OrdinalIgnoreCase));
    }

    // ── JSON helpers ────────────────────────────────────────────────────────────────────────────

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static List<string> ReadStringArray(JsonElement element, string propertyName)
    {
        var result = new List<string>();
        if (!element.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } text)
                result.Add(text);
        }

        return result;
    }
}
