using Microsoft.Extensions.Configuration;
using Xunit;

namespace StudioTechBI.API.Tests;

/// <summary>Regression tests for Priority 1 of the staging-readiness plan: a Staging environment
/// must never silently inherit Production's configuration values just because it lacks its own
/// override. Loads the real appsettings files the same way Program.cs does via
/// WebApplication.CreateBuilder's ASPNETCORE_ENVIRONMENT convention (base + appsettings.
/// {Environment}.json), so this fails if a future change reintroduces a hardcoded production
/// value into the base file.</summary>
public class EnvironmentConfigurationTests
{
    private const string ProductionRedirectUri =
        "https://studiotechbi-api-acekguf6eqajd2gg.australiasoutheast-01.azurewebsites.net/api/connections/oauth/onedrive/callback";

    private static IConfiguration BuildConfiguration(string? environmentJsonFileName)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false);

        if (environmentJsonFileName != null)
        {
            builder.AddJsonFile(environmentJsonFileName, optional: false);
        }

        return builder.Build();
    }

    [Fact]
    public void BaseConfiguration_DoesNotHardcodeTheProductionRedirectUri()
    {
        // If no environment-specific file is loaded at all (shouldn't happen for a real
        // deployment, but guards the base file directly), the base file itself must not carry
        // the production value as a "default everyone inherits".
        var configuration = BuildConfiguration(environmentJsonFileName: null);

        Assert.NotEqual(ProductionRedirectUri, configuration["MicrosoftAuth:RedirectUri"]);
    }

    [Fact]
    public void StagingConfiguration_DoesNotResolveToTheProductionRedirectUri()
    {
        var configuration = BuildConfiguration("appsettings.Staging.json");

        Assert.NotEqual(ProductionRedirectUri, configuration["MicrosoftAuth:RedirectUri"]);
    }

    [Fact]
    public void DevelopmentConfiguration_DoesNotResolveToTheProductionRedirectUri()
    {
        var configuration = BuildConfiguration("appsettings.Development.json");

        Assert.NotEqual(ProductionRedirectUri, configuration["MicrosoftAuth:RedirectUri"]);
        Assert.Equal("http://localhost:5000/api/connections/oauth/onedrive/callback", configuration["MicrosoftAuth:RedirectUri"]);
    }

    [Fact]
    public void StagingConfiguration_KeepsPasswordResetTokenExposureDisabled()
    {
        var configuration = BuildConfiguration("appsettings.Staging.json");

        Assert.False(configuration.GetValue<bool>("PasswordReset:ExposeTokenInResponse"));
    }
}
