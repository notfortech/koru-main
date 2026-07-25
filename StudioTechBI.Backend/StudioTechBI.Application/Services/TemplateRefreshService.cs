using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.DTOs.TemplateRefresh;
using StudioTechBI.Application.DTOs.Templates;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;

namespace StudioTechBI.Application.Services;

/// <summary>
/// Composes existing, unmodified services (ITemplateMatchingService, IClientResolver,
/// ITemplateService, IAuditLogService) with the new ITemplateRebindClient. No changes to any of
/// those services were needed or made — this is purely new orchestration on top of them.
/// </summary>
public sealed class TemplateRefreshService : ITemplateRefreshService
{
    private const double InstantRefreshConfidenceThreshold = 0.8;

    private readonly ITemplateMatchingService _matching;
    private readonly ITemplateRebindClient _rebindClient;
    private readonly IClientResolver _clientResolver;
    private readonly ITemplateService _templates;
    private readonly IAuditLogService _auditLog;
    private readonly IOptionsMonitor<TemplateCatalogOptions> _catalogOptions;
    private readonly ILogger<TemplateRefreshService> _logger;

    public TemplateRefreshService(
        ITemplateMatchingService matching,
        ITemplateRebindClient rebindClient,
        IClientResolver clientResolver,
        ITemplateService templates,
        IAuditLogService auditLog,
        IOptionsMonitor<TemplateCatalogOptions> catalogOptions,
        ILogger<TemplateRefreshService> logger)
    {
        _matching = matching;
        _rebindClient = rebindClient;
        _clientResolver = clientResolver;
        _templates = templates;
        _auditLog = auditLog;
        _catalogOptions = catalogOptions;
        _logger = logger;
    }

    public async Task<TemplateRefreshMatchResponse> MatchAsync(TemplateMatchRequest request, CancellationToken cancellationToken = default)
    {
        var match = await _matching.MatchFromBlobAsync(
            request.ClientCode, request.UseSelectedClient, request.BlobPath, request.MaxRows, request.UseAiRefinement, cancellationToken);

        var topConfidence = match.Templates.Count > 0 ? match.Templates[0].Confidence : 0;

        await _auditLog.WriteAsync(new AuditLogEntryDto
        {
            TenantId = null,
            Action = "SchemaMatched",
            EntityType = "Template",
            EntityId = match.Templates.Count > 0 ? match.Templates[0].TemplateId.ToString() : null,
            NewValue = System.Text.Json.JsonSerializer.Serialize(new
            {
                match.ClientCode,
                match.BlobPath,
                TopConfidence = topConfidence,
                CandidateCount = match.Templates.Count
            })
        }, cancellationToken);

        return new TemplateRefreshMatchResponse
        {
            Match = match,
            ReadyForInstantRefresh = topConfidence >= InstantRefreshConfidenceThreshold
        };
    }

    public async Task<TemplateRefreshRebindResponse> RebindAsync(TemplateRefreshRebindRequest request, CancellationToken cancellationToken = default)
    {
        var client = await _clientResolver.ResolveAsync(request.ClientCode, cancellationToken)
            ?? throw new InvalidOperationException("Client not found.");

        var template = await _templates.GetByIdAsync(request.TemplateId, cancellationToken)
            ?? throw new InvalidOperationException($"Template '{request.TemplateId}' not found.");

        var correlationId = Guid.NewGuid().ToString();
        var rebindRequest = new TemplateRebindRequest(
            ClientName: $"Client - {client.ClientName}",
            TemplateWorkspaceName: _catalogOptions.CurrentValue.MasterWorkspaceName,
            TemplateDatasetName: template.TemplateName,
            SourceFilePath: request.SourceFilePath);

        _logger.LogInformation(
            "TemplateRefresh.Rebind.Requested CorrelationId={CorrelationId} ClientCode={ClientCode} TemplateId={TemplateId}",
            correlationId, request.ClientCode, request.TemplateId);

        var result = await _rebindClient.RebindAsync(rebindRequest, correlationId, cancellationToken);

        await _auditLog.WriteAsync(new AuditLogEntryDto
        {
            TenantId = null,
            Action = "DatasetRebound",
            EntityType = "Template",
            EntityId = request.TemplateId.ToString(),
            NewValue = System.Text.Json.JsonSerializer.Serialize(new
            {
                ClientCode = request.ClientCode,
                result.WorkspaceId,
                result.DatasetId,
                result.RefreshTriggered
            })
        }, cancellationToken);

        return new TemplateRefreshRebindResponse(
            result.WorkspaceId, result.WorkspaceName, result.DatasetId, result.DatasetName, result.RefreshTriggered, result.Steps,
            result.ReportId, result.ReportName, result.ReportCloned);
    }
}
