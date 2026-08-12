using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.Constants;
using StudioTechBI.Application.DTOs.ReportGenerator;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Services;

/// <summary>
/// Reads templates/html/index.json + every listed manifest.json and pushes the registry to
/// ReportAgent.Api. Extracted out of HtmlTemplateRegistrySyncService's timer loop (which now just
/// calls this on an interval) so the exact same logic can also run on demand -- e.g. an admin's
/// "Sync Now" action right after uploading or editing a template, instead of waiting up to 5
/// minutes for the next scheduled cycle.
/// </summary>
public sealed class HtmlTemplateSyncRunner : IHtmlTemplateSyncRunner
{
    private readonly IBlobStorageService _blobStorage;
    private readonly IReportGeneratorClient _reportGeneratorClient;
    private readonly ILogger<HtmlTemplateSyncRunner> _logger;

    public HtmlTemplateSyncRunner(
        IBlobStorageService blobStorage,
        IReportGeneratorClient reportGeneratorClient,
        ILogger<HtmlTemplateSyncRunner> logger)
    {
        _blobStorage = blobStorage;
        _reportGeneratorClient = reportGeneratorClient;
        _logger = logger;
    }

    public async Task<HtmlTemplateSyncResult> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        // Ids an admin has explicitly deleted via HtmlTemplateAdminService.DeleteAsync, for seed
        // ids specifically -- un-listing from index.json alone can't stop a seed template from
        // reappearing below, since the seed catalog is compiled into the assembly, not blob-backed.
        var excludedSeedIds = await LoadExcludedSeedIdsAsync(cancellationToken);

        // Keyed by id so a properly-uploaded "clients"/templates/html/<id>/manifest.json always
        // wins over its built-in seed counterpart of the same id (HtmlTemplateSeedCatalog) — the
        // seed exists purely so the 2 already-onboarded templates (currently living as flat .html
        // files in the pre-existing "report-templates" container instead of the originally
        // designed convention) still match, without requiring anyone to re-upload anything.
        var manifestsById = new Dictionary<string, HtmlTemplateManifestPushDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in HtmlTemplateSeedCatalog.Templates)
        {
            if (excludedSeedIds.Contains(seed.Id, StringComparer.OrdinalIgnoreCase)) continue;
            manifestsById[seed.Id] = new HtmlTemplateManifestPushDto(seed.Id, seed.ManifestJson);
        }

        List<string>? templateIds = null;
        await using (var indexStream = await _blobStorage.DownloadBlobAsync(HtmlTemplateBlobPaths.IndexPath, cancellationToken))
        {
            if (indexStream is null)
            {
                _logger.LogInformation(
                    "HtmlTemplateRegistrySync.NoIndex — {Path} not found yet; pushing {SeedCount} built-in seed manifest(s) only.",
                    HtmlTemplateBlobPaths.IndexPath, manifestsById.Count);
            }
            else
            {
                templateIds = await JsonSerializer.DeserializeAsync<List<string>>(indexStream, cancellationToken: cancellationToken);
            }
        }

        var blobResolvedCount = 0;
        if (templateIds is { Count: > 0 })
        {
            foreach (var id in templateIds)
            {
                var manifestPath = HtmlTemplateBlobPaths.ManifestPath(id);
                await using var manifestStream = await _blobStorage.DownloadBlobAsync(manifestPath, cancellationToken);
                if (manifestStream is null)
                {
                    _logger.LogWarning(
                        "HtmlTemplateRegistrySync.ManifestMissing TemplateId={TemplateId} Path={Path} — listed in " +
                        "the index but no manifest.json found; skipped for this cycle.",
                        id, manifestPath);
                    continue;
                }

                using var reader = new StreamReader(manifestStream, Encoding.UTF8);
                var manifestJson = await reader.ReadToEndAsync(cancellationToken);
                manifestsById[id] = new HtmlTemplateManifestPushDto(id, manifestJson);
                blobResolvedCount++;
            }
        }

        if (manifestsById.Count == 0)
        {
            _logger.LogWarning("HtmlTemplateRegistrySync.NoManifestsResolved — nothing to push this cycle.");
            return new HtmlTemplateSyncResult(0, 0, 0);
        }

        var manifests = manifestsById.Values.ToList();
        await _reportGeneratorClient.PushHtmlTemplateRegistryAsync(
            manifests, correlationId: Guid.NewGuid().ToString("N"), cancellationToken);

        // A future onboarding mistake (right id added to index.json, but the manifest/chrome files
        // land in the wrong container/path) is otherwise invisible here -- everything just quietly
        // falls back to whatever seed exists, or drops out of the registry. Surfacing the
        // blob-vs-seed split every cycle turns that into a visible, at-a-glance log signal instead.
        var seedFallbackCount = manifestsById.Count - blobResolvedCount;
        _logger.LogInformation(
            "HtmlTemplateRegistrySync.Pushed Count={Count} BlobResolved={BlobResolved} SeedFallback={SeedFallback} ExcludedSeeds={ExcludedSeeds}",
            manifests.Count, blobResolvedCount, seedFallbackCount, excludedSeedIds.Count);

        return new HtmlTemplateSyncResult(manifests.Count, blobResolvedCount, seedFallbackCount);
    }

    private async Task<List<string>> LoadExcludedSeedIdsAsync(CancellationToken cancellationToken)
    {
        await using var stream = await _blobStorage.DownloadBlobAsync(HtmlTemplateBlobPaths.ExcludedSeedIdsPath, cancellationToken);
        if (stream is null) return new List<string>();

        var ids = await JsonSerializer.DeserializeAsync<List<string>>(stream, cancellationToken: cancellationToken);
        return ids ?? new List<string>();
    }
}
