using Microsoft.Extensions.Configuration;
using Xunit;

namespace StudioTechBI.API.Tests;

/// <summary>Regression tests for Priority 3 of the staging-readiness plan: no environment may
/// silently fall back to a hardcoded/production database. Loads the real appsettings files the
/// same way Program.cs does, mirroring EnvironmentConfigurationTests.</summary>
public class DatabaseConfigurationSafetyTests
{
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
    public void BaseConfiguration_NeverCommitsARealConnectionString()
    {
        var configuration = BuildConfiguration(environmentJsonFileName: null);

        Assert.True(string.IsNullOrEmpty(configuration.GetConnectionString("DefaultConnection")));
    }

    [Fact]
    public void StagingConfiguration_UsesTheRealDatabasePath_NotDemoStorage()
    {
        var configuration = BuildConfiguration("appsettings.Staging.json");

        Assert.False(configuration.GetValue<bool>("UseDemoStorage"));
    }
}
