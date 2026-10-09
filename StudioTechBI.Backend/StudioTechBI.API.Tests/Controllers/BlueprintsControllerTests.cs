using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StudioTechBI.API.Controllers;
using StudioTechBI.API.Tests.TestHelpers;
using StudioTechBI.Application.DTOs.Blueprints;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.Credits;
using StudioTechBI.Application.DTOs.InsightsEngine;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using Xunit;

namespace StudioTechBI.API.Tests.Controllers;

/// <summary>Regression tests for the cross-tenant Blueprint IDOR fix (Priority 0.1). Covers every
/// scenario in the task's acceptance checklist for GetById/GetPdf/GetJson/GetAiSummary/Delete.</summary>
public class BlueprintsControllerTests
{
    private static readonly Guid OwnClientId = Guid.NewGuid();
    private static readonly Guid OtherClientId = Guid.NewGuid();
    private static readonly Guid BlueprintId = Guid.NewGuid();

    private Mock<IAiGateway> _gateway = null!;
    private Mock<IClientAccessGuard> _accessGuard = null!;
    private Mock<ILocalCreditLedgerService> _localCredits = null!;
    private Mock<IInsightsEngineReportInsightsClient> _reportInsights = null!;

    private BlueprintsController CreateController(bool ownsClient)
    {
        _gateway = new Mock<IAiGateway>();
        _accessGuard = new Mock<IClientAccessGuard>();
        _accessGuard.Setup(x => x.CanAccessClientAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), OwnClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _accessGuard.Setup(x => x.CanAccessClientAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), OtherClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _localCredits = new Mock<ILocalCreditLedgerService>();
        _localCredits.Setup(x => x.CheckAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocalCreditResult(true, 10, null));

        _reportInsights = new Mock<IInsightsEngineReportInsightsClient>();

        var clientService = new Mock<IClientService>();
        var optionsMonitor = new Mock<IOptionsMonitor<InsightsEngineOptions>>();
        optionsMonitor.Setup(x => x.CurrentValue).Returns(new InsightsEngineOptions { ExternalCopilotAiEnabled = true });

        var blueprintClientId = ownsClient ? OwnClientId : OtherClientId;
        _gateway.Setup(x => x.GetBlueprintAsync(BlueprintId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlueprintDto { Id = BlueprintId, ClientId = blueprintClientId });
        _gateway.Setup(x => x.GetBlueprintPdfAsync(BlueprintId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));
        _gateway.Setup(x => x.GetBlueprintJsonAsync(BlueprintId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"meta\":{}}");
        _gateway.Setup(x => x.DeleteBlueprintAsync(BlueprintId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new BlueprintsController(
            _gateway.Object,
            clientService.Object,
            _accessGuard.Object,
            _reportInsights.Object,
            optionsMonitor.Object,
            _localCredits.Object,
            NullLogger<BlueprintsController>.Instance);

        controller.SetUser(ControllerTestHelpers.AuthenticatedUser("owner@firm.com"));
        return controller;
    }

    [Fact]
    public async Task GetById_OwnBlueprint_Succeeds()
    {
        var controller = CreateController(ownsClient: true);
        var result = await controller.GetById(BlueprintId, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetById_OtherTenantsBlueprint_Returns403()
    {
        var controller = CreateController(ownsClient: false);
        var result = await controller.GetById(BlueprintId, CancellationToken.None);
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objResult.StatusCode);
    }

    [Fact]
    public async Task GetById_NonexistentBlueprint_Returns404()
    {
        var controller = CreateController(ownsClient: true);
        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetPdf_OtherTenantsBlueprint_Returns403_AndNeverFetchesThePdf()
    {
        var controller = CreateController(ownsClient: false);
        var result = await controller.GetPdf(BlueprintId, CancellationToken.None);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objResult.StatusCode);
        _gateway.Verify(x => x.GetBlueprintPdfAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPdf_OwnBlueprint_Succeeds()
    {
        var controller = CreateController(ownsClient: true);
        var result = await controller.GetPdf(BlueprintId, CancellationToken.None);
        Assert.IsType<FileStreamResult>(result);
    }

    [Fact]
    public async Task GetJson_OtherTenantsBlueprint_Returns403_AndNeverFetchesTheJson()
    {
        var controller = CreateController(ownsClient: false);
        var result = await controller.GetJson(BlueprintId, CancellationToken.None);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objResult.StatusCode);
        _gateway.Verify(x => x.GetBlueprintJsonAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetJson_OwnBlueprint_Succeeds()
    {
        var controller = CreateController(ownsClient: true);
        var result = await controller.GetJson(BlueprintId, CancellationToken.None);
        Assert.IsType<ContentResult>(result);
    }

    [Fact]
    public async Task GetAiSummary_OtherTenantsBlueprint_Returns403_AndConsumesZeroCredits()
    {
        var controller = CreateController(ownsClient: false);
        var result = await controller.GetAiSummary(BlueprintId, null, CancellationToken.None);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objResult.StatusCode);

        // The critical assertion: no credit check AND no credit consumption happened for the
        // victim tenant when the caller doesn't own the blueprint.
        _localCredits.Verify(x => x.CheckAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _localCredits.Verify(x => x.ConsumeAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _reportInsights.Verify(x => x.GetInsightsFromMetadataAsync(It.IsAny<ReportPageInsightsRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAiSummary_OwnBlueprint_ConsumesCreditsAndSucceeds()
    {
        _reportInsights = new Mock<IInsightsEngineReportInsightsClient>();
        var controller = CreateController(ownsClient: true);
        _reportInsights.Setup(x => x.GetInsightsFromMetadataAsync(It.IsAny<ReportPageInsightsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReportPageInsightsResponse { Provider = "test", Summary = "ok", Insights = new List<string>(), FollowUps = new List<string>() });

        var result = await controller.GetAiSummary(BlueprintId, null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        _localCredits.Verify(x => x.ConsumeAsync(OwnClientId, It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_OtherTenantsBlueprint_Returns403_AndNeverDeletes()
    {
        var controller = CreateController(ownsClient: false);
        var result = await controller.Delete(BlueprintId, CancellationToken.None);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objResult.StatusCode);
        _gateway.Verify(x => x.DeleteBlueprintAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_OwnBlueprint_Succeeds()
    {
        var controller = CreateController(ownsClient: true);
        var result = await controller.Delete(BlueprintId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _gateway.Verify(x => x.DeleteBlueprintAsync(BlueprintId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        // [Authorize] is enforced by the ASP.NET Core auth middleware pipeline, not reachable by
        // calling an action directly -- confirmed present and unchanged by this fix via reflection.
        var attr = typeof(BlueprintsController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true);
        Assert.NotEmpty(attr);
    }
}
