using System.Text.Json;

namespace StudioTechBI.Application.Utilities;

/// <summary>
/// Distinguishes the two blueprint shapes DashboardTemplateController.GenerateAsync now accepts
/// in its single `blueprint` form field: the stbi_transformers-generated Analytics Blueprint
/// (top-level `data_model`) and the richer "Design Blueprint" format (top-level `templateId` +
/// `pages`, no `data_model`) — the premium/medium-tier shape from the industry template design
/// packs, whose visuals already carry real field bindings instead of needing axis inference.
/// The two shapes are structurally disjoint on these keys, so detection needs no new request field.
/// </summary>
public static class BlueprintFormatDetector
{
    public static bool IsDesignBlueprint(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty("templateId", out _)
        && root.TryGetProperty("pages", out _)
        && !root.TryGetProperty("data_model", out _);
}
