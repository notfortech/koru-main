using StudioTechBI.Application.DTOs.Templates;

namespace StudioTechBI.Application.Interfaces;

public interface IInsightsEngineTemplateMappingClient
{
    Task<TemplateMappingPreview> RefineTemplateMappingAsync(
        IReadOnlyList<string> clientColumns,
        IReadOnlyList<string> requiredTemplateColumns,
        IReadOnlyList<string> optionalTemplateColumns,
        CancellationToken ct = default);
}

