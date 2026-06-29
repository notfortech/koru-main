using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.Blueprint;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.Services;

public sealed class BlueprintService : IBlueprintService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ApplicationDbContext _db;
    private readonly IStbAgentHostClient _agentHost;
    private readonly IBlobStorageService _blob;
    private readonly IClientService _clientService;
    private readonly ILogger<BlueprintService> _logger;

    public BlueprintService(
        ApplicationDbContext db,
        IStbAgentHostClient agentHost,
        IBlobStorageService blob,
        IClientService clientService,
        ILogger<BlueprintService> logger)
    {
        _db = db;
        _agentHost = agentHost;
        _blob = blob;
        _clientService = clientService;
        _logger = logger;
    }

    public async Task<BlueprintGenerateResponseDto> GenerateAsync(
        string clientCode,
        string businessRequirement,
        string? industry,
        string? existingSchema,
        string requestedByEmail,
        CancellationToken ct = default)
    {
        var client = await _clientService.GetByClientCodeOrIdAsync(clientCode, ct)
            ?? throw new InvalidOperationException($"Client '{clientCode}' not found.");

        var effectiveIndustry = (!string.IsNullOrWhiteSpace(industry) ? industry : client.Industry)
            ?? string.Empty;

        var blueprint = new Blueprint
        {
            Id = Guid.NewGuid(),
            ClientId = client.ClientId,
            BusinessRequirement = businessRequirement,
            Industry = effectiveIndustry,
            ExistingSchema = string.IsNullOrWhiteSpace(existingSchema) ? null : existingSchema,
            Status = "Pending",
            RequestedByEmail = requestedByEmail
        };

        _db.Blueprints.Add(blueprint);
        await _db.SaveChangesAsync(ct);

        var agentRequest = new StbAgentHostRequestDto
        {
            BusinessRequirement = businessRequirement,
            Industry = effectiveIndustry,
            ExistingSchema = blueprint.ExistingSchema
        };

        StbAgentHostResponseDto agentResponse;
        try
        {
            var tenantId = client.ClientCode ?? client.ClientId.ToString();
            var tenantName = client.ClientName;
            agentResponse = await _agentHost.GenerateBlueprintAsync(agentRequest, tenantId, tenantName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StbAgentHost call failed for client {ClientCode}", clientCode);
            blueprint.Status = "Failed";
            blueprint.ErrorMessage = ex.Message;
            blueprint.CompletedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw new InvalidOperationException("Blueprint generation service is temporarily unavailable.", ex);
        }

        // Store full response JSON to Azure Blob for AI training purposes.
        string? blobPath = null;
        try
        {
            var responseJson = JsonSerializer.Serialize(agentResponse, JsonOptions);
            var blobName = $"{clientCode}/blueprints/{blueprint.Id}/response.json";
            using var jsonStream = new MemoryStream(Encoding.UTF8.GetBytes(responseJson));
            await _blob.UploadClientBlobAsync(blobName, jsonStream, "application/json");
            blobPath = blobName;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store blueprint response JSON to blob for blueprint {Id}", blueprint.Id);
        }

        blueprint.Status = agentResponse.Status ?? "Completed";
        blueprint.PdfDownloadUrl = agentResponse.PdfDownloadUrl;
        blueprint.CreditsRemaining = agentResponse.CreditsRemaining;
        blueprint.CreditsConsumed = agentResponse.CreditsConsumed;
        blueprint.SubscriptionPlan = agentResponse.SubscriptionPlan;
        blueprint.ResetDate = agentResponse.ResetDate;
        blueprint.ResponseBlobPath = blobPath;
        blueprint.WarningsJson = agentResponse.Warnings?.Count > 0
            ? JsonSerializer.Serialize(agentResponse.Warnings)
            : null;
        blueprint.CompletedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new BlueprintGenerateResponseDto
        {
            RequestId = blueprint.Id,
            Status = blueprint.Status,
            PdfDownloadUrl = blueprint.PdfDownloadUrl,
            CreditsConsumed = agentResponse.CreditsConsumed,
            CreditsRemaining = agentResponse.CreditsRemaining,
            SubscriptionPlan = agentResponse.SubscriptionPlan,
            ResetDate = agentResponse.ResetDate,
            Warnings = agentResponse.Warnings ?? new List<string>()
        };
    }

    public async Task<BlueprintCreditsDto?> GetCreditsAsync(string clientCode, CancellationToken ct = default)
    {
        var client = await _clientService.GetByClientCodeOrIdAsync(clientCode, ct);
        if (client == null) return null;

        var latest = await _db.Blueprints
            .AsNoTracking()
            .Where(b => b.ClientId == client.ClientId && !b.IsDeleted && b.CreditsRemaining != null)
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (latest == null) return null;

        return new BlueprintCreditsDto
        {
            CreditsRemaining = latest.CreditsRemaining ?? 0,
            CreditsConsumed = latest.CreditsConsumed ?? 0,
            SubscriptionPlan = latest.SubscriptionPlan,
            ResetDate = latest.ResetDate
        };
    }

    public async Task<IReadOnlyList<Blueprint>> GetRequestsAsync(string clientCode, CancellationToken ct = default)
    {
        var client = await _clientService.GetByClientCodeOrIdAsync(clientCode, ct);
        if (client == null) return Array.Empty<Blueprint>();

        return await _db.Blueprints
            .AsNoTracking()
            .Where(b => b.ClientId == client.ClientId && !b.IsDeleted)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Stream?> GetPdfStreamAsync(string clientCode, Guid blueprintId, CancellationToken ct = default)
    {
        var client = await _clientService.GetByClientCodeOrIdAsync(clientCode, ct);
        if (client == null) return null;

        var blueprint = await _db.Blueprints
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == blueprintId && b.ClientId == client.ClientId && !b.IsDeleted, ct);

        if (blueprint == null || string.IsNullOrWhiteSpace(blueprint.PdfDownloadUrl))
            return null;

        return await _agentHost.DownloadPdfAsync(blueprint.PdfDownloadUrl, ct);
    }
}
