using Microsoft.AspNetCore.Http;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StudioTechBI.API.Controllers;
using StudioTechBI.API.Tests.TestHelpers;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Application.Services;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Clients;
using Xunit;

namespace StudioTechBI.API.Tests.Controllers;

/// <summary>Regression tests for the cross-tenant blob-access IDOR fix in
/// InsightsEngineController.SuggestTransformationsFromBlob (Priority 0.3), plus the same latent
/// gap fixed in its sibling SuggestModelsFromBlob.</summary>
public class InsightsEngineControllerTests
{
    private static readonly Client OwnClient = new() { Id = Guid.NewGuid(), ClientCode = "AU-001", BlobFolderPath = "AU-001" };
    private static readonly Client OtherClient = new() { Id = Guid.NewGuid(), ClientCode = "AU-002", BlobFolderPath = "AU-002" };
    private const string OwnBlobPath = "AU-001/accounting/created/data.csv";
    private const string OtherClientsBlobPath = "AU-002/accounting/created/secret.csv";

    private Mock<IBlobStorageService> _blobStorage = null!;
    private Mock<IClientResolver> _clientResolver = null!;
    private Mock<IClientByCompanyQuery> _clientByCompanyQuery = null!;

    private InsightsEngineController CreateController()
    {
        _blobStorage = new Mock<IBlobStorageService>();
        _blobStorage.Setup(x => x.DownloadBlobAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes("Col1,Col2\nA,1\nB,2\n")));

        _clientResolver = new Mock<IClientResolver>();
        _clientResolver.Setup(x => x.ResolveAsync(OwnClient.Id.ToString(), It.IsAny<CancellationToken>())).ReturnsAsync(OwnClient);
        _clientResolver.Setup(x => x.ResolveAsync(OtherClient.Id.ToString(), It.IsAny<CancellationToken>())).ReturnsAsync(OtherClient);
        // SuggestModelsFromBlob resolves by ClientCode, not by raw Id -- mock both lookup keys.
        _clientResolver.Setup(x => x.ResolveAsync(OwnClient.ClientCode!, It.IsAny<CancellationToken>())).ReturnsAsync(OwnClient);
        _clientResolver.Setup(x => x.ResolveAsync(OtherClient.ClientCode!, It.IsAny<CancellationToken>())).ReturnsAsync(OtherClient);

        // Caller only ever owns OwnClient — mirrors ClientAccessGuard's own email->company lookup,
        // exercised here through the controller's private CanAccessClientAsync/ResolveTargetClientAsync.
        _clientByCompanyQuery = new Mock<IClientByCompanyQuery>();
        _clientByCompanyQuery.Setup(x => x.GetClientsForUserEmailAsync("owner@firm.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Application.DTOs.Auth.ClientInfoFromCompanyDto>
            {
                new() { ClientId = OwnClient.Id, ClientCode = OwnClient.ClientCode! }
            });

        var clientService = new Mock<IClientService>();
        var templateMatching = new Mock<ITemplateMatchingService>();
        templateMatching.Setup(x => x.GetBestCatalogMatchScoreAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.0);

        var templateVerification = new Mock<IInsightTemplateVerificationService>();
        templateVerification
            .Setup(x => x.EnrichWithVerifiedTemplatesAsync(It.IsAny<Application.DTOs.InsightsEngine.TransformSuggestResponse>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Application.DTOs.InsightsEngine.InsightsWithTemplatesResponse());

        var optionsMonitor = new Mock<IOptionsMonitor<InsightsEngineOptions>>();
        // ExternalCopilotAiEnabled defaults false -> takes the catalog-only branch, so the
        // InsightsEngineClient/HttpClient below is constructed but never actually invoked.
        optionsMonitor.Setup(x => x.CurrentValue).Returns(new InsightsEngineOptions());

        var controller = new InsightsEngineController(
            new InsightsEngineClient(new HttpClient(), NullLogger<InsightsEngineClient>.Instance),
            new DataSamplingService(_blobStorage.Object),
            templateVerification.Object,
            _blobStorage.Object,
            _clientResolver.Object,
            _clientByCompanyQuery.Object,
            clientService.Object,
            templateMatching.Object,
            optionsMonitor.Object,
            NullLogger<InsightsEngineController>.Instance);

        controller.SetUser(ControllerTestHelpers.AuthenticatedUser("owner@firm.com"));
        return controller;
    }

    // ── SuggestTransformationsFromBlob ──────────────────────────────────────────

    [Fact]
    public async Task SuggestTransformationsFromBlob_OwnClientAndOwnBlob_Succeeds_AndReadsTheBlob()
    {
        var controller = CreateController();
        var body = new InsightsEngineController.SuggestFromBlobApiRequest
        {
            ClientId = OwnClient.Id.ToString(),
            BlobPath = OwnBlobPath
        };

        var result = await controller.SuggestTransformationsFromBlob(body, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        _blobStorage.Verify(x => x.DownloadBlobAsync(OwnBlobPath, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SuggestTransformationsFromBlob_ForeignClientId_Returns403_AndNeverReadsAnyBlob()
    {
        var controller = CreateController();
        var body = new InsightsEngineController.SuggestFromBlobApiRequest
        {
            ClientId = OtherClient.Id.ToString(),
            BlobPath = OtherClientsBlobPath
        };

        var result = await controller.SuggestTransformationsFromBlob(body, CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
        _blobStorage.Verify(x => x.DownloadBlobAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SuggestTransformationsFromBlob_OwnClientIdButAnotherClientsBlobPath_Returns403_AndNeverReadsTheBlob()
    {
        var controller = CreateController();
        var body = new InsightsEngineController.SuggestFromBlobApiRequest
        {
            ClientId = OwnClient.Id.ToString(),
            BlobPath = OtherClientsBlobPath // mixing own ClientId with someone else's file path
        };

        var result = await controller.SuggestTransformationsFromBlob(body, CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
        _blobStorage.Verify(x => x.DownloadBlobAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void SuggestTransformationsFromBlob_Unauthenticated_HasAuthorizeAttribute()
    {
        // [Authorize] on InsightsEngineController is enforced by the ASP.NET Core auth
        // middleware pipeline, not reachable by calling the action directly -- confirmed present
        // and unchanged by this fix via reflection instead of a full integration-test host.
        var attr = typeof(InsightsEngineController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true);
        Assert.NotEmpty(attr);
    }

    // ── SuggestModelsFromBlob (same vulnerability class, fixed alongside the endpoint above) ───

    [Fact]
    public async Task SuggestModelsFromBlob_ExplicitlySuppliedForeignBlobPath_Returns403_AndNeverReadsTheBlob()
    {
        var controller = CreateController();
        var request = new Application.DTOs.InsightsEngine.ModelSuggestFromBlobRequest
        {
            ClientCode = OwnClient.ClientCode,
            UseSelectedClient = false,
            BlobPath = OtherClientsBlobPath, // own client, but someone else's file
            MaxRows = 100
        };

        var result = await controller.SuggestModelsFromBlob(request, CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
        _blobStorage.Verify(x => x.DownloadBlobAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SuggestModelsFromBlob_OwnClientAndOwnBlob_Succeeds_AndReadsTheBlob()
    {
        var controller = CreateController();
        var request = new Application.DTOs.InsightsEngine.ModelSuggestFromBlobRequest
        {
            ClientCode = OwnClient.ClientCode,
            UseSelectedClient = false,
            BlobPath = OwnBlobPath,
            MaxRows = 100
        };

        var result = await controller.SuggestModelsFromBlob(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        _blobStorage.Verify(x => x.DownloadBlobAsync(OwnBlobPath, It.IsAny<CancellationToken>()), Times.Once);
    }
}
