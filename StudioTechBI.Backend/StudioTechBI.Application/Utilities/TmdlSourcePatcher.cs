using System.Text.RegularExpressions;
using StudioTechBI.Application.DTOs.ReportDesigner;

namespace StudioTechBI.Application.Utilities;

/// <summary>
/// Patches an authored TMDL file set's data-source expression to point at a real, fetchable
/// URL instead of the placeholder local path — mirrors the external deploy_agent.py prototype's
/// patch_m_expressions step. Every template in DashboardTemplateLibrary uses the convention:
/// `expression SourceFilePath = "&lt;path&gt;" meta [IsParameterQuery=true, ...]` — one shared
/// parameter referenced by every table's `Excel.Workbook(File.Contents(SourceFilePath), ...)`
/// partition, so patching happens once per file set, not per table.
/// Caveat: stbi_transformers' live LLM-authored TMDL has no guarantee of using this exact
/// parameter name — this never throws, it just reports whether a match was found.
/// </summary>
public static class TmdlSourcePatcher
{
    private static readonly Regex SourceFilePathPattern = new(
        "(expression\\s+SourceFilePath\\s*=\\s*)\"[^\"]*\"",
        RegexOptions.Compiled);

    public static List<TmdlFileDto> PatchSourceFilePath(List<TmdlFileDto> files, string newSourceUrl, out bool patched)
    {
        patched = false;
        var result = new List<TmdlFileDto>(files.Count);

        foreach (var file in files)
        {
            if (!patched && SourceFilePathPattern.IsMatch(file.Content))
            {
                var patchedContent = SourceFilePathPattern.Replace(
                    file.Content,
                    m => $"{m.Groups[1].Value}\"{newSourceUrl}\"",
                    1);
                result.Add(file with { Content = patchedContent });
                patched = true;
            }
            else
            {
                result.Add(file);
            }
        }

        return result;
    }
}
