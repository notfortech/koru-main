using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StudioTechBI.API.Controllers;
using StudioTechBI.API.Tests.TestHelpers;
using StudioTechBI.Application.DTOs.BindDeploy;
using StudioTechBI.Application.DTOs.ReportDesigner;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Options;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;
using StudioTechBI.Infrastructure.Services;
using Xunit;

namespace StudioTechBI.API.Tests.Controllers;

/// <summary>Regression tests for the cross-tenant IDOR fix in ReportDesignerController
/// (Priority 0.2) — consent/generate-model/match/publish/data-usage-consent must all reject a
/// caller-supplied ClientId the caller doesn't own, strictly before any consent/credit/queue/
/// Power BI side effect.</summary>
public class ReportDesignerControllerTests
{
    private static readonly Client OwnClient = new() { Id = Guid.NewGuid(), ClientCode = "AU-001", ClientName = "Own Firm" };
    private static readonly Client OtherClient = new() { Id = Guid.NewGuid(), ClientCode = "AU-002", ClientName = "Other Firm" };

    private Mock<IClientResolver> _clientResolver = null!;
    private Mock<IClientAccessGuard> _accessGuard = null!;
    private Mock<IReportDesignerConsentService> _consentService = null!;
    private Mock<IReportDataUsageConsentService> _dataUsageConsentService = null!;
    private Mock<IAgentHostClient> _agentHostClient = null!;
    private Mock<IReportModelGenerationQueue> _reportModelQueue = null!;
    private Mock<ISchemaModelMatchQueue> _schemaModelMatchQueue = null!;
    private Mock<IBindDeployClient> _bindDeployClient = null!;
    private Mock<IPowerBiAssetWriter> _powerBiAssetWriter = null!;
    private Mock<ITemplateService> _templates = null!;
    private Mock<IReportDesignerClient> _reportDesignerClient = null!;

    private ReportDesignerController CreateController(string requestedClientCode)
    {
        _clientResolver = new Mock<IClientResolver>();
        _clientResolver.Setup(x => x.ResolveAsync(OwnClient.ClientCode!, It.IsAny<CancellationToken>())).ReturnsAsync(OwnClient);
        _clientResolver.Setup(x => x.ResolveAsync(OtherClient.ClientCode!, It.IsAny<CancellationToken>())).ReturnsAsync(OtherClient);

        _accessGuard = new Mock<IClientAccessGuard>();
        _accessGuard.Setup(x => x.CanAccessClientAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), OwnClient.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _accessGuard.Setup(x => x.CanAccessClientAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), OtherClient.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _consentService = new Mock<IReportDesignerConsentService>();
        _consentService.Setup(x => x.HasConsentAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        _dataUsageConsentService = new Mock<IReportDataUsageConsentService>();
        _dataUsageConsentService.Setup(x => x.RecordConsentAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.UtcNow);

        _agentHostClient = new Mock<IAgentHostClient>();
        _reportModelQueue = new Mock<IReportModelGenerationQueue>();
        _schemaModelMatchQueue = new Mock<ISchemaModelMatchQueue>();

        _bindDeployClient = new Mock<IBindDeployClient>();
        _bindDeployClient.Setup(x => x.DeployDatasetAsync(It.IsAny<DeployDatasetRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeployDatasetResult("ws-1", "Workspace 1", "ds-1", "Dataset 1", true, new List<string>()));

        _powerBiAssetWriter = new Mock<IPowerBiAssetWriter>();
        _templates = new Mock<ITemplateService>();
        _reportDesignerClient = new Mock<IReportDesignerClient>();
        _reportDesignerClient.Setup(x => x.AuthorTmdlAsync(It.IsAny<JsonElement>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorTmdlResponse(new List<TmdlFileDto>(), "reasoning", new TmdlValidationDto(true, new List<string>())));

        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var controller = new ReportDesignerController(
            _reportDesignerClient.Object,
            _bindDeployClient.Object,
            null!, // SqlSchemaReaderService -- unused by consent/generate-model/match/publish/data-usage-consent
            null!, // SharePointSchemaService -- unused by consent/generate-model/match/publish/data-usage-consent
            _agentHostClient.Object,
            _consentService.Object,
            _dataUsageConsentService.Object,
            _clientResolver.Object,
            _accessGuard.Object,
            Mock.Of<ITemplateRefreshService>(),
            _templates.Object,
            _powerBiAssetWriter.Object,
            Mock.Of<ILocalCreditLedgerService>(),
            _reportModelQueue.Object,
            _schemaModelMatchQueue.Object,
            new ApplicationDbContext(dbOptions),
            NullLogger<ReportDesignerController>.Instance,
            Options.Create(new UploadLimitsOptions()));

        controller.SetUser(ControllerTestHelpers.AuthenticatedUser("owner@firm.com"));
        return controller;
    }

    private static ExtractedSchemaDto MinimalSchema() => new(
        Source: "excel", FileName: "data.xlsx",
        Tables: new List<TableSchemaDto> { new("Sheet1", "Sheet1", 10, new List<ColumnSchemaDto> { new("Col1", "string", true, null) }) },
        SchemaHash: "hash-1", ExtractedAt: DateTimeOffset.UtcNow);

    // ── consent ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Consent_OwnClient_Succeeds()
    {
        var controller = CreateController(OwnClient.ClientCode!);
        var result = await controller.RecordConsentAsync(new ReportDesignerConsentRequest(OwnClient.ClientCode!, "hash-1", true), CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Consent_OtherTenantsClient_Returns403()
    {
        var controller = CreateController(OtherClient.ClientCode!);
        var result = await controller.RecordConsentAsync(new ReportDesignerConsentRequest(OtherClient.ClientCode!, "hash-1", true), CancellationToken.None);
        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
    }

    // ── generate-model ───────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateModel_OtherTenantsClient_Returns403_AndNeverChecksConsentOrQueues()
    {
        var controller = CreateController(OtherClient.ClientCode!);
        var result = await controller.GenerateReportModelAsync(
            new GenerateReportModelRequest(OtherClient.ClientCode!, MinimalSchema(), null), CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
        _consentService.Verify(x => x.HasConsentAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _agentHostClient.Verify(x => x.CheckCreditsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── match ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Match_OtherTenantsClient_Returns403_AndNeverEnqueues()
    {
        var controller = CreateController(OtherClient.ClientCode!);
        var result = await controller.MatchAsync(new ReportMatchRequest(OtherClient.ClientCode!, MinimalSchema()), CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
        _schemaModelMatchQueue.Verify(x => x.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── publish (the critical one — must never reach Power BI for an unauthorized ClientId) ────

    [Fact]
    public async Task Publish_OtherTenantsClient_Returns403_AndCallsPowerBiZeroTimes()
    {
        var controller = CreateController(OtherClient.ClientCode!);
        var request = new PublishReportRequest(OtherClient.ClientCode!, JsonDocument.Parse("{}").RootElement);

        var result = await controller.PublishAsync(request, CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
        _bindDeployClient.Verify(x => x.DeployDatasetAsync(It.IsAny<DeployDatasetRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _powerBiAssetWriter.Verify(x => x.WriteAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _reportDesignerClient.Verify(x => x.AuthorTmdlAsync(It.IsAny<JsonElement>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Publish_OwnClient_DeploysToPowerBi()
    {
        var controller = CreateController(OwnClient.ClientCode!);
        var request = new PublishReportRequest(OwnClient.ClientCode!, JsonDocument.Parse("{}").RootElement);

        var result = await controller.PublishAsync(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        _bindDeployClient.Verify(x => x.DeployDatasetAsync(It.IsAny<DeployDatasetRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── data-usage-consent ───────────────────────────────────────────────────

    [Fact]
    public async Task DataUsageConsent_OtherTenantsClient_Returns403_AndNeverRecordsConsent()
    {
        var controller = CreateController(OtherClient.ClientCode!);
        var result = await controller.RecordDataUsageConsentAsync(Guid.NewGuid(), new DataUsageConsentRequest(OtherClient.ClientCode!), CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
        _dataUsageConsentService.Verify(x => x.RecordConsentAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DataUsageConsent_OwnClient_Succeeds()
    {
        var controller = CreateController(OwnClient.ClientCode!);
        var result = await controller.RecordDataUsageConsentAsync(Guid.NewGuid(), new DataUsageConsentRequest(OwnClient.ClientCode!), CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        var attr = typeof(ReportDesignerController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true);
        Assert.NotEmpty(attr);
    }
}
