using System.Text.Json.Nodes;

namespace StudioTechBI.Application.Utilities;

/// <summary>
/// The structural files a PBIP project needs beyond report.json/.platform and the TMDL semantic
/// model — added after a live Power BI Import API rejection (RequestedFileIsEncryptedOrCorrupted)
/// confirmed the zip was missing the pieces that actually make a folder set into a *project*:
/// the top-level .pbip pointer file, and definition.pbir (the file that links the Report to its
/// Semantic Model by relative path — without it, Power BI has nothing telling it what data the
/// report's visuals belong to). The Semantic Model folder also needs its own .platform manifest,
/// not just the Report folder's.
/// Same caveat as everywhere else this was built: best-effort against Microsoft's documented PBIP
/// shape, not verified against a successful import in this session — but now informed by a real
/// rejection reason rather than pure guesswork.
/// </summary>
public static class PbipSkeletonBuilder
{
    /// <summary>The top-level "{slug}.pbip" file — the project pointer Power BI's import looks
    /// for at the zip root, naming which folder holds the report.</summary>
    public static string BuildTopLevelProjectJson(string reportFolderName)
    {
        var root = new JsonObject
        {
            ["version"] = "1.0",
            ["artifacts"] = new JsonArray
            {
                new JsonObject { ["report"] = new JsonObject { ["path"] = reportFolderName } }
            },
            ["settings"] = new JsonObject { ["enableAutoRecovery"] = true }
        };
        return root.ToJsonString();
    }

    /// <summary>"{slug}.Report/definition.pbir" — links the report to its semantic model by
    /// relative path within the same zip. This is the file that was missing entirely; without it
    /// the report has no dataset to bind its visuals to.</summary>
    public static string BuildReportDefinitionPbir(string semanticModelFolderName)
    {
        var root = new JsonObject
        {
            ["version"] = "4.0",
            ["datasetReference"] = new JsonObject
            {
                ["byPath"] = new JsonObject { ["path"] = $"../{semanticModelFolderName}" },
                ["byConnection"] = null
            }
        };
        return root.ToJsonString();
    }

    /// <summary>"{slug}.SemanticModel/.platform" — same fixed PBIP shape as the Report folder's
    /// manifest, but type "SemanticModel" and its own logicalId.</summary>
    public static string BuildSemanticModelPlatformManifest(string displayName)
    {
        var root = new JsonObject
        {
            ["$schema"] = "https://developer.microsoft.com/json-schemas/fabric/gitIntegration/platformProperties/2.0.0/schema.json",
            ["metadata"] = new JsonObject { ["type"] = "SemanticModel", ["displayName"] = displayName },
            ["config"] = new JsonObject { ["version"] = "2.0", ["logicalId"] = Guid.NewGuid().ToString() }
        };
        return root.ToJsonString();
    }
}
