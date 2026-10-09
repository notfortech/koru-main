using Moq;
using StudioTechBI.API.Tests.TestHelpers;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.DTOs.Auth;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Infrastructure.Services;
using Xunit;

namespace StudioTechBI.API.Tests;

/// <summary>Unit tests for the extracted ownership check reused by BlueprintsController,
/// ReportDesignerController, and InsightsEngineController's SuggestTransformationsFromBlob.</summary>
public class ClientAccessGuardTests
{
    private static ClientAccessGuard CreateGuard(
        Mock<IClientByCompanyQuery> byCompany,
        Mock<IClientService> clientService) =>
        new(byCompany.Object, clientService.Object);

    [Fact]
    public async Task ReturnsTrue_WhenClientIsAmongUsersCompanyClients()
    {
        var clientId = Guid.NewGuid();
        var byCompany = new Mock<IClientByCompanyQuery>();
        byCompany.Setup(x => x.GetClientsForUserEmailAsync("a@firm.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClientInfoFromCompanyDto> { new() { ClientId = clientId, ClientCode = "AU-001" } });
        var clientService = new Mock<IClientService>();

        var guard = CreateGuard(byCompany, clientService);
        var user = ControllerTestHelpers.AuthenticatedUser("a@firm.com");

        Assert.True(await guard.CanAccessClientAsync(user, clientId));
    }

    [Fact]
    public async Task ReturnsFalse_WhenClientBelongsToSomeoneElse()
    {
        var ownClientId = Guid.NewGuid();
        var otherClientId = Guid.NewGuid();
        var byCompany = new Mock<IClientByCompanyQuery>();
        byCompany.Setup(x => x.GetClientsForUserEmailAsync("a@firm.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClientInfoFromCompanyDto> { new() { ClientId = ownClientId, ClientCode = "AU-001" } });
        var clientService = new Mock<IClientService>();

        var guard = CreateGuard(byCompany, clientService);
        var user = ControllerTestHelpers.AuthenticatedUser("a@firm.com");

        Assert.False(await guard.CanAccessClientAsync(user, otherClientId));
    }

    [Fact]
    public async Task FallsBackToClientCodeClaim_WhenNoCompanyLinkExists()
    {
        var clientId = Guid.NewGuid();
        var byCompany = new Mock<IClientByCompanyQuery>();
        byCompany.Setup(x => x.GetClientsForUserEmailAsync("a@firm.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClientInfoFromCompanyDto>());
        var clientService = new Mock<IClientService>();
        clientService.Setup(x => x.GetByClientCodeOrIdAsync("AU-002", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClientDto { ClientId = clientId, ClientCode = "AU-002" });

        var guard = CreateGuard(byCompany, clientService);
        var user = ControllerTestHelpers.AuthenticatedUser("a@firm.com", clientCode: "AU-002");

        Assert.True(await guard.CanAccessClientAsync(user, clientId));
    }

    [Fact]
    public async Task ReturnsFalse_WhenUserHasNoEmailClaim()
    {
        var byCompany = new Mock<IClientByCompanyQuery>();
        var clientService = new Mock<IClientService>();
        var guard = CreateGuard(byCompany, clientService);

        var identity = new System.Security.Claims.ClaimsIdentity();
        var user = new System.Security.Claims.ClaimsPrincipal(identity);

        Assert.False(await guard.CanAccessClientAsync(user, Guid.NewGuid()));
        byCompany.Verify(x => x.GetClientsForUserEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
