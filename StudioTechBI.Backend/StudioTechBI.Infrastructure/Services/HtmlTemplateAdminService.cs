using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.Constants;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.DTOs.ReportGenerator;
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
    private static readonly Regex NormalizeKeyRegex = new("[^a-z0-9]", RegexOptions.Compiled);
    private static readonly string[] ReferenceDatasetExtensions = [".xlsx", ".csv"];
    private static readonly JsonSerializerOptions PrettyPrint = new() { WriteIndented = true };

    private readonly IBlobStorageService _blobStorage;
    private readonly IHtmlTemplateSyncRunner _syncRunner;
    private readonly IReportGeneratorClient _reportGeneratorClient;
    private readonly IHtmlReportAssemblyService _htmlAssembly;
    private readonly ILogger<HtmlTemplateAdminService> _logger;

    public HtmlTemplateAdminService(
        IBlobStorageService blobStorage,
        IHtmlTemplateSyncRunner syncRunner,
        IReportGeneratorClient reportGeneratorClient,
        IHtmlReportAssemblyService htmlAssembly,
        ILogger<HtmlTemplateAdminService> logger)
    {
        _blobStorage = blobStorage;
        _syncRunner = syncRunner;
        _reportGeneratorClient = reportGeneratorClient;
        _htmlAssembly = htmlAssembly;
        _logger = logger;
    }

    public async Task<HtmlTemplateBulkUploadResponseDto> UploadBatchAsync(Stream zipStream, bool dryRun = false, CancellationToken cancellationToken = default)
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
            return new HtmlTemplateBulkUploadResponseDto(results, 0, 0, 1, dryRun);
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

                if (!dryRun)
                {
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
                }

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

        if (anySucceeded && !dryRun)
        {
            try
            {
                await SaveIndexAsync(mergedIds, cancellationToken);

                // Re-trigger the sync cycle immediately so a replaced/new template is live for
                // matching right away instead of waiting up to 5 minutes -- mirrors
                // UpdateManifestAsync's identical fail-soft trigger below. Without this, a client
                // re-uploading the same template id (replacing it) would have its old blob
                // content served to matching for up to 5 minutes even though the admin list
                // already reflects the new one.
                try
                {
                    await _syncRunner.RunOnceAsync(cancellationToken);
                }
                catch (Exception syncEx)
                {
                    _logger.LogWarning(syncEx, "HtmlTemplateAdmin.UploadBatch.SyncFailed");
                }
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
        return new HtmlTemplateBulkUploadResponseDto(results, created, updated, failed, dryRun);
    }

    public async Task<HtmlTemplateUploadResultDto> UploadChromeHtmlAsync(string templateId, string chromeHtml, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateId))
            return new HtmlTemplateUploadResultDto(templateId ?? "", "Failed", new List<string> { "A template id is required." }, new List<string>());

        if (string.IsNullOrWhiteSpace(chromeHtml))
            return new HtmlTemplateUploadResultDto(templateId, "Failed", new List<string> { "The uploaded file is empty." }, new List<string>());

        var ids = await LoadIndexAsync(cancellationToken);
        if (!ids.Contains(templateId, StringComparer.Ordinal))
        {
            return new HtmlTemplateUploadResultDto(
                templateId, "Failed",
                new List<string> { $"Template '{templateId}' does not exist -- a single chrome.html file can only replace an existing template. Upload a full .zip (manifest.json + chrome.html) to create a new one." },
                new List<string>());
        }

        await using var manifestStream = await _blobStorage.DownloadBlobAsync(HtmlTemplateBlobPaths.ManifestPath(templateId), cancellationToken);
        if (manifestStream is null)
        {
            return new HtmlTemplateUploadResultDto(
                templateId, "Failed",
                new List<string> { "manifest.json not found for this template -- cannot validate the new chrome.html against its testIds contract." },
                new List<string>());
        }

        JsonDocument manifestDoc;
        try
        {
            manifestDoc = await JsonDocument.ParseAsync(manifestStream, cancellationToken: cancellationToken);
        }
        catch (JsonException jsonEx)
        {
            return new HtmlTemplateUploadResultDto(
                templateId, "Failed", new List<string> { $"This template's existing manifest.json is not valid JSON: {jsonEx.Message}" }, new List<string>());
        }

        using (manifestDoc)
        {
            var (isValid, errors, warnings) = ValidateChromeHtml(chromeHtml, manifestDoc.RootElement);
            if (!isValid)
                return new HtmlTemplateUploadResultDto(templateId, "Failed", errors, warnings);

            using var chromeBytes = new MemoryStream(Encoding.UTF8.GetBytes(chromeHtml));
            await _blobStorage.UploadClientBlobAsync(HtmlTemplateBlobPaths.ChromeHtmlPath(templateId), chromeBytes, "text/html", cancellationToken);

            try
            {
                await _syncRunner.RunOnceAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HtmlTemplateAdmin.UploadChromeHtml.SyncFailed TemplateId={TemplateId}", templateId);
            }

            return new HtmlTemplateUploadResultDto(templateId, "Updated", new List<string>(), warnings);
        }
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
                var hasReferenceDataset = await FindReferenceDatasetExtensionAsync(id, cancellationToken) is not null;

                result.Add(new HtmlTemplateSummaryDto(id, name, industry, required, optional, hasPreview, hasThemeSlots, false, null, hasReferenceDataset));
            }
            catch (Exception ex)
            {
                result.Add(new HtmlTemplateSummaryDto(id, id, "", new List<string>(), new List<string>(), false, false, true, $"manifest.json failed to parse: {ex.Message}"));
            }
        }

        return result;
    }

    public async Task<bool> DeleteAsync(string templateId, CancellationToken cancellationToken = default)
    {
        var ids = await LoadIndexAsync(cancellationToken);
        var updated = ids.Where(id => !string.Equals(id, templateId, StringComparison.Ordinal)).ToList();
        var wasListed = updated.Count != ids.Count;
        var isSeedTemplate = HtmlTemplateSeedCatalog.Find(templateId) is not null;

        if (!wasListed && !isSeedTemplate)
        {
            // Nothing matched -- return false so the controller can report 404 instead of a
            // silent no-op "success" that leaves the caller thinking a delete happened.
            return false;
        }

        if (wasListed)
            await SaveIndexAsync(updated, cancellationToken);

        // Built-in seed templates (HtmlTemplateSeedCatalog) are compiled into the assembly, not
        // blob-backed -- un-listing from index.json does nothing to stop HtmlTemplateSyncRunner
        // from unconditionally re-adding them every cycle (including the very re-sync triggered
        // a few lines below). Without this, a "deleted" seed template keeps being pushed to
        // ReportAgent.Api and can keep winning matches forever, while the admin UI shows it gone.
        if (isSeedTemplate)
            await ExcludeSeedIdAsync(templateId, cancellationToken);

        // Re-trigger the sync cycle immediately, same fail-soft pattern as UploadBatchAsync/
        // UpdateManifestAsync -- an unlisted template stops matching right away instead of
        // waiting up to 5 minutes.
        try
        {
            await _syncRunner.RunOnceAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HtmlTemplateAdmin.Delete.SyncFailed TemplateId={TemplateId}", templateId);
        }

        return true;
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

    public async Task<HtmlTemplateReferenceDatasetDeriveResponseDto> DeriveManifestFromReferenceDatasetAsync(
        string templateId, Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(templateId))
            return new HtmlTemplateReferenceDatasetDeriveResponseDto(templateId ?? "", false, null, warnings, new List<string> { "A template id is required." });

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!ReferenceDatasetExtensions.Contains(ext))
            return new HtmlTemplateReferenceDatasetDeriveResponseDto(templateId, false, null, warnings, new List<string> { "Only .xlsx or .csv files are supported." });

        var ids = await LoadIndexAsync(cancellationToken);
        if (!ids.Contains(templateId, StringComparer.Ordinal))
        {
            return new HtmlTemplateReferenceDatasetDeriveResponseDto(
                templateId, false, null, warnings,
                new List<string> { $"Template '{templateId}' does not exist -- upload a full .zip to create it first." });
        }

        await using var manifestStream = await _blobStorage.DownloadBlobAsync(HtmlTemplateBlobPaths.ManifestPath(templateId), cancellationToken);
        if (manifestStream is null)
        {
            return new HtmlTemplateReferenceDatasetDeriveResponseDto(
                templateId, false, null, warnings, new List<string> { "manifest.json not found for this template." });
        }

        using var manifestDoc = await JsonDocument.ParseAsync(manifestStream, cancellationToken: cancellationToken);

        // Buffer once so the same bytes can be stored to blob and sent for profiling without
        // depending on the caller's stream being seekable/re-readable.
        using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, cancellationToken);
        var fileBytes = buffer.ToArray();

        // Stored immediately, independent of whether the derived manifest below is ever saved --
        // this file also backs PreviewWithReferenceDatasetAsync. Any stale reference dataset under
        // a different extension from a prior upload is removed so ListAsync/Preview don't find two.
        foreach (var staleExt in ReferenceDatasetExtensions.Where(e => e != ext))
            await _blobStorage.DeleteBlobIfExistsAsync(HtmlTemplateBlobPaths.ReferenceDatasetPath(templateId, staleExt), cancellationToken);

        using (var storeStream = new MemoryStream(fileBytes))
        {
            await _blobStorage.UploadClientBlobAsync(
                HtmlTemplateBlobPaths.ReferenceDatasetPath(templateId, ext), storeStream,
                ext == ".csv" ? "text/csv" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                cancellationToken);
        }

        ColumnProfileResultDto profile;
        try
        {
            using var profileStream = new MemoryStream(fileBytes);
            profile = await _reportGeneratorClient.ProfileColumnsAsync(profileStream, fileName, Guid.NewGuid().ToString(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HtmlTemplateAdmin.DeriveManifest.ProfileFailed TemplateId={TemplateId}", templateId);
            return new HtmlTemplateReferenceDatasetDeriveResponseDto(
                templateId, false, null, warnings, new List<string> { $"Could not profile the reference dataset: {ex.Message}" });
        }

        var mergedManifestJson = MergeManifestWithProfile(manifestDoc.RootElement, profile, warnings);
        return new HtmlTemplateReferenceDatasetDeriveResponseDto(templateId, true, mergedManifestJson, warnings, new List<string>());
    }

    public async Task<HtmlTemplatePreviewResponseDto> PreviewWithReferenceDatasetAsync(
        string templateId, CancellationToken cancellationToken = default)
    {
        var ext = await FindReferenceDatasetExtensionAsync(templateId, cancellationToken);
        if (ext is null)
        {
            return new HtmlTemplatePreviewResponseDto(
                templateId, false, null, new List<string>(), "No reference dataset has been uploaded for this template yet.");
        }

        await using var blobStream = await _blobStorage.DownloadBlobAsync(HtmlTemplateBlobPaths.ReferenceDatasetPath(templateId, ext), cancellationToken);
        if (blobStream is null)
            return new HtmlTemplatePreviewResponseDto(templateId, false, null, new List<string>(), "The stored reference dataset could not be read.");

        using var buffer = new MemoryStream();
        await blobStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var correlationId = Guid.NewGuid().ToString();
        var fileName = $"reference-dataset{ext}";

        GeneratedReportDto result;
        try
        {
            // Neither templateId (the unrelated generic KPI/chart engine's own registry, which
            // always has a catch-all fallback) nor htmlTemplateId is forced here -- this must
            // exercise the real auto-pick path exactly as a client's matching upload would, or it
            // wouldn't prove anything about the deterministic matcher actually choosing this
            // template on its own.
            result = await _reportGeneratorClient.GenerateReportAsync(
                buffer, fileName, templateId: null, filtersJson: null, htmlTemplateId: null, correlationId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HtmlTemplateAdmin.Preview.GenerateFailed TemplateId={TemplateId}", templateId);
            return new HtmlTemplatePreviewResponseDto(templateId, false, null, new List<string>(), $"Report generation failed: {ex.Message}");
        }

        if (!string.Equals(result.HtmlTemplateId, templateId, StringComparison.Ordinal))
        {
            var message = result.HtmlTemplateId is null
                ? "The reference dataset did not deterministically match this template -- review requiredColumns/requires against the uploaded file."
                : $"The reference dataset matched a different template ('{result.HtmlTemplateId}') instead of this one -- another template's requiredColumns/requires may overlap.";
            return new HtmlTemplatePreviewResponseDto(templateId, false, null, result.Warnings, message);
        }

        result = await _htmlAssembly.AssembleAsync(result, themeOverride: null, cancellationToken);
        return new HtmlTemplatePreviewResponseDto(templateId, true, result.HtmlReport, result.Warnings, null);
    }

    // ── Reference dataset merge ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Merges a profiled reference dataset into an existing manifest. The dataset is always the
    /// source of truth: requiredColumns becomes exactly the flat, de-duplicated set of every real
    /// column found across every table/sheet in the file (full override, not a union), and
    /// requires.min* is overwritten with the real aggregated counts. Anything the manifest
    /// previously declared that the dataset doesn't account for at all is demoted into
    /// optionalColumns rather than dropped. dataContract.rowFields (single- or multi-table, per the
    /// manifest's own already-declared shape) is append-only -- existing aliases are never
    /// touched (chrome.html's hardcoded JS depends on them staying stable). Returns the proposed
    /// manifest as pretty-printed JSON; never writes anything.
    /// </summary>
    private static string MergeManifestWithProfile(JsonElement manifestElement, ColumnProfileResultDto profile, List<string> warnings)
    {
        var manifest = JsonNode.Parse(manifestElement.GetRawText())!.AsObject();

        var existingRequired = ReadStringArray(manifestElement, "requiredColumns");
        var existingOptional = ReadStringArray(manifestElement, "optionalColumns");
        var fileColumnNames = profile.Columns.Select(c => c.Name).ToList();
        var fileNormalizedKeys = new HashSet<string>(fileColumnNames.Select(NormalizeKey));

        // requiredColumns/optionalColumns feed the AI-assisted scoring's column-overlap check
        // against a client's WHOLE uploaded file (html_template_matcher._score_template), never
        // scoped to one table -- so this stays a flat union across every sheet regardless of
        // whether the manifest itself is single- or multi-table.
        var newRequired = DedupeByNormalizedKey(fileColumnNames, seen: null, out var requiredKeys);
        manifest["requiredColumns"] = new JsonArray(newRequired.Select(n => (JsonNode)n).ToArray());

        var newOptional = DedupeByNormalizedKey(existingOptional.Concat(existingRequired), seen: requiredKeys, out _);
        manifest["optionalColumns"] = new JsonArray(newOptional.Select(n => (JsonNode)n).ToArray());

        var requires = manifest["requires"]?.AsObject() ?? new JsonObject();
        requires["minNumeric"] = profile.Counts.MinNumeric;
        requires["minDate"] = profile.Counts.MinDate;
        requires["minCategorical"] = profile.Counts.MinCategorical;
        manifest["requires"] = requires;

        var dataContract = manifest["dataContract"]?.AsObject();
        if (dataContract is not null && dataContract["rowFields"] is JsonArray rowFieldsArray)
        {
            // Single-table export (row_export.py) only ever reads from pick_primary_table()'s one
            // table -- never the flat cross-sheet union -- so this must scope to just that table's
            // columns, or we'd append rowFields entries for columns that will never actually be
            // present in an exported report.
            var primaryColumns = profile.Tables.TryGetValue(profile.PrimaryTable, out var pc) ? pc : profile.Columns;
            MergeRowFields(rowFieldsArray, primaryColumns, warnings, tableLabel: null);
        }
        else if (dataContract?["tables"] is JsonArray tablesArray && tablesArray.Count > 0)
        {
            foreach (var tableNode in tablesArray)
            {
                var tableObj = tableNode?.AsObject();
                var tableName = tableObj?["table"]?.GetValue<string>();
                if (tableObj is null || string.IsNullOrWhiteSpace(tableName)) continue;

                var sheetColumns = FindTableColumns(profile.Tables, tableName);
                if (sheetColumns is null)
                {
                    warnings.Add($"dataContract.tables '{tableName}' has no matching sheet in the reference dataset -- its rowFields were not updated.");
                    continue;
                }

                if (tableObj["rowFields"] is JsonArray tableRowFields)
                    MergeRowFields(tableRowFields, sheetColumns, warnings, tableLabel: tableName);
            }
        }

        // Flag anything the old manifest declared that the reference dataset doesn't account for
        // anywhere -- requiredColumns/optionalColumns above already fully reflect the dataset, so
        // this is purely informational about what moved.
        foreach (var name in existingRequired.Concat(existingOptional))
        {
            var key = NormalizeKey(name);
            if (key.Length > 0 && !fileNormalizedKeys.Contains(key))
                warnings.Add($"'{name}' was in this template's manifest but was not found anywhere in the reference dataset -- moved to optionalColumns.");
        }

        return manifest.ToJsonString(PrettyPrint);
    }

    /// <summary>De-duplicates by normalized key, preserving first-seen original casing/spelling.
    /// `seen` (if given) is treated as already-claimed keys to skip -- used to keep optionalColumns
    /// from re-listing anything already in the new requiredColumns.</summary>
    private static List<string> DedupeByNormalizedKey(IEnumerable<string> names, HashSet<string>? seen, out HashSet<string> keysOut)
    {
        var result = new List<string>();
        keysOut = seen is null ? new HashSet<string>() : new HashSet<string>(seen);
        foreach (var name in names)
        {
            var key = NormalizeKey(name);
            if (key.Length == 0 || !keysOut.Add(key)) continue;
            result.Add(name);
        }
        return result;
    }

    /// <summary>Append-only merge of one dataContract rowFields array against one table's profiled
    /// columns -- never renames/removes an existing alias, only appends entries for genuinely new
    /// columns, and warns (scoped to `tableLabel` when given) about any existing entry whose column
    /// isn't in that table's profiled columns.</summary>
    private static void MergeRowFields(JsonArray rowFieldsArray, IEnumerable<ProfiledColumnDto> tableColumns, List<string> warnings, string? tableLabel)
    {
        var existingNormalizedKeys = new HashSet<string>();
        var existingAliases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in rowFieldsArray)
        {
            if (field?["column"]?.GetValue<string>() is { } column) existingNormalizedKeys.Add(NormalizeKey(column));
            if (field?["alias"]?.GetValue<string>() is { } alias) existingAliases.Add(alias);
        }

        var tableColumnKeys = new HashSet<string>();
        foreach (var col in tableColumns)
        {
            var key = NormalizeKey(col.Name);
            tableColumnKeys.Add(key);
            if (!existingNormalizedKeys.Add(key)) continue;

            // Alias defaults to the raw column name verbatim, not a camelCase transform -- a
            // template's chrome.html reads row data by whatever key row_export.py exports under
            // (literally the declared alias), and every template's chrome.html seen so far reads
            // the original snake_case/whatever-case column names directly, not a camelCase
            // rewrite. Inventing a different casing here would silently produce a key chrome.html
            // never looks up -- exactly the all-zero-KPI bug this default is fixing.
            var alias = col.Name;
            var candidate = alias;
            for (var suffix = 2; !existingAliases.Add(candidate); suffix++)
                candidate = $"{alias}{suffix}";

            rowFieldsArray.Add(new JsonObject { ["column"] = col.Name, ["alias"] = candidate, ["role"] = col.Role });
        }

        var label = tableLabel is null ? "" : $"table '{tableLabel}' ";
        foreach (var field in rowFieldsArray)
        {
            if (field?["column"]?.GetValue<string>() is not { } column) continue;
            if (tableColumnKeys.Contains(NormalizeKey(column))) continue;
            warnings.Add($"dataContract {label}rowFields column '{column}' was not found in the reference dataset -- verify it's still correct.");
        }
    }

    private static List<ProfiledColumnDto>? FindTableColumns(Dictionary<string, List<ProfiledColumnDto>> tables, string tableName)
    {
        if (tables.TryGetValue(tableName, out var exact)) return exact;
        var ciKey = tables.Keys.FirstOrDefault(k => string.Equals(k, tableName, StringComparison.OrdinalIgnoreCase));
        return ciKey is not null ? tables[ciKey] : null;
    }

    private static string NormalizeKey(string? s) => string.IsNullOrEmpty(s) ? "" : NormalizeKeyRegex.Replace(s.Trim().ToLowerInvariant(), "");

    private async Task<string?> FindReferenceDatasetExtensionAsync(string templateId, CancellationToken cancellationToken)
    {
        foreach (var ext in ReferenceDatasetExtensions)
        {
            if (await _blobStorage.BlobExistsAsync(HtmlTemplateBlobPaths.ReferenceDatasetPath(templateId, ext), cancellationToken))
                return ext;
        }
        return null;
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

            // Two supported shapes, mirroring row_export.py's is_multi_table_contract() on the
            // Python engine side: single-table ({"rowFields": [...]})  for a template whose data
            // fits one flat array, or multi-table ({"tables": [{"table", "rowFields", ...}, ...]})
            // for a template spanning several sheets/tables at different grains. A manifest must
            // declare exactly one of the two -- this was previously hard-requiring the single-
            // table shape even for templates correctly using the multi-table one, rejecting every
            // multi-table upload with "Missing 'dataContract.rowFields' array."
            var hasRowFields = dataContract.TryGetProperty("rowFields", out var rowFields) && rowFields.ValueKind == JsonValueKind.Array;
            var hasTables = dataContract.TryGetProperty("tables", out var tables) && tables.ValueKind == JsonValueKind.Array && tables.GetArrayLength() > 0;

            if (hasTables)
            {
                var index = 0;
                foreach (var table in tables.EnumerateArray())
                {
                    if (table.ValueKind != JsonValueKind.Object)
                    {
                        errors.Add($"'dataContract.tables[{index}]' must be an object.");
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(GetString(table, "table")))
                            errors.Add($"'dataContract.tables[{index}]' is missing a 'table' name.");
                        if (!table.TryGetProperty("rowFields", out var tableRowFields) || tableRowFields.ValueKind != JsonValueKind.Array)
                            errors.Add($"'dataContract.tables[{index}]' is missing a 'rowFields' array.");
                    }
                    index++;
                }
            }
            else if (!hasRowFields)
            {
                errors.Add(
                    "'dataContract' must declare either a 'rowFields' array (single-table) or a " +
                    "non-empty 'tables' array (multi-table).");
            }
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

        // Must be the literal marker HtmlReportAssemblyService substitutes -- not just any mention
        // of "stbi-report-data" (e.g. a template author's own pre-authored, empty
        // id="stbi-report-data" script tag satisfies a loose substring check but never actually
        // receives injected data: assembly's marker-missing fallback would append a second element
        // with the same id, and getElementById returns the FIRST match in document order -- the
        // original empty one, not ours -- so the report would render with zero rows despite a
        // confident match and a passed validation. Requiring the real marker here is what actually
        // guarantees the template will receive data, not just reference the idea of it.
        if (!chromeHtml.Contains(HtmlTemplateBlobPaths.DataMarker, StringComparison.Ordinal))
            errors.Add($"chrome.html does not contain the {HtmlTemplateBlobPaths.DataMarker} marker -- it will not receive injected report data.");

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

    private async Task ExcludeSeedIdAsync(string templateId, CancellationToken cancellationToken)
    {
        await using var stream = await _blobStorage.DownloadBlobAsync(HtmlTemplateBlobPaths.ExcludedSeedIdsPath, cancellationToken);
        var excluded = stream is null
            ? new List<string>()
            : await JsonSerializer.DeserializeAsync<List<string>>(stream, cancellationToken: cancellationToken) ?? new List<string>();

        if (excluded.Contains(templateId, StringComparer.Ordinal)) return;

        excluded.Add(templateId);
        var json = JsonSerializer.Serialize(excluded, PrettyPrint);
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await _blobStorage.UploadClientBlobAsync(HtmlTemplateBlobPaths.ExcludedSeedIdsPath, bytes, "application/json", cancellationToken);
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
