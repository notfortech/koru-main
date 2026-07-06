using StudioTechBI.Application.DTOs.ReportDesigner;

namespace StudioTechBI.Application.Interfaces;

public interface IReportDesignerClient
{
    Task<GenerateReportModelResponse> GenerateReportModelAsync(
        GenerateReportModelRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}
