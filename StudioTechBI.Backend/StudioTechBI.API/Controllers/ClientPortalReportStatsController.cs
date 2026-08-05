using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.Dashboard;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.API.Controllers;

/// <summary>
/// Backs the client dashboard's "View Report Stats" quick action — aggregates Saved Reports,
/// deterministic vs. AI-assisted generation counts (ReportGeneratorController.GenerateAsync writes
/// ReportGenerationEvent per genuinely new report), AI credits consumed, and the live credit
/// balance, in one round trip.
/// </summary>
[ApiController]
[Route("api/client-portal/report-stats")]
[Authorize]
public sealed class ClientPortalReportStatsController : ControllerBase
{
    // Must match ReportDesignerController's private ReportModelFeature constant -- the only
    // credit-consuming step in the AI-assisted Report Generator flow today (see
    // ReportDesignerController.cs, GenerateReportModelAsync). No shared constant exists between
    // controllers in this codebase (same pattern as HtmlMatchConfidenceThreshold mirroring
    // DashboardTemplateController.MatchConfidenceThreshold) -- flagged here so it isn't drifted
    // silently if that feature tag ever changes.
    private const string ReportModelFeature = "report-model-generation";

    private readonly ApplicationDbContext _db;
    private readonly IClientService _clientService;
    private readonly IAgentHostClient _agentHostClient;
    private readonly ILocalCreditLedgerService _localCredits;

    public ClientPortalReportStatsController(
        ApplicationDbContext db, IClientService clientService, IAgentHostClient agentHostClient,
        ILocalCreditLedgerService localCredits)
    {
        _db = db;
        _clientService = clientService;
        _agentHostClient = agentHostClient;
        _localCredits = localCredits;
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        var client = await ResolveClientAsync(cancellationToken);
        if (client is null)
            return BadRequest(ApiResponse<object>.ErrorResponse("A resolvable client_code claim is required."));

        var savedReportsCount = await _db.SavedReports
            .CountAsync(r => r.ClientId == client.ClientId && r.Status != "Archived", cancellationToken);

        var deterministicCount = await _db.ReportGenerationEvents
            .CountAsync(e => e.ClientId == client.ClientId && e.Mode == ReportGenerationModes.Deterministic, cancellationToken);

        var aiAssistedCount = await _db.ReportGenerationEvents
            .CountAsync(e => e.ClientId == client.ClientId && e.Mode == ReportGenerationModes.AiAssisted, cancellationToken);

        var usage = await _agentHostClient.GetUsageSummaryAsync(client.ClientId, ReportModelFeature, cancellationToken);

        // Local interim balance (see LocalCreditLedgerService) -- AgentHost's own ledger is
        // currently bypassed, so this is the real, per-client number today. Kept consistent with
        // BlueprintsLegacyController.GetCredits, the other place this balance is read.
        var creditsRemaining = await _localCredits.GetBalanceAsync(client.ClientId, cancellationToken);

        var dto = new ReportStatsDto(
            SavedReportsCount: savedReportsCount,
            DeterministicReportsCount: deterministicCount,
            AiAssistedReportsCount: aiAssistedCount,
            AiCreditsConsumed: usage.TotalCreditsConsumed,
            CreditsRemaining: creditsRemaining,
            IsUnlimited: false,
            ResetDate: null);

        return Ok(ApiResponse<ReportStatsDto>.SuccessResponse(dto));
    }

    private async Task<ClientDto?> ResolveClientAsync(CancellationToken ct)
    {
        var clientCode = User.FindFirstValue("client_code")?.Trim();
        if (string.IsNullOrWhiteSpace(clientCode))
            return null;

        return await _clientService.GetByClientCodeOrIdAsync(clientCode, ct);
    }
}
