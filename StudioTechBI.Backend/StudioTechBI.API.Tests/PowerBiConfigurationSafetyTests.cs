using Microsoft.Extensions.Configuration;
using Xunit;

namespace StudioTechBI.API.Tests;

/// <summary>Regression test for Priority 4 of the staging-readiness plan: no Power BI credential
/// or workspace/report/dataset id may ever be committed as a real value. Workspace resolution
/// itself is verified safe by code review (PowerBiAssetQuery resolves entirely from the running
/// environment's own database, never from a client-name heuristic) -- not something a config test
/// can usefully assert, so it isn't re-tested here.</summary>
public class PowerBiConfigurationSafetyTests
{
    [Fact]
    public void BaseConfiguration_NeverCommitsRealPowerBiCredentialsOrIds()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var keys = new[] { "ClientId", "ClientSecret", "TenantId", "WorkspaceId", "ReportId", "DatasetId" };
        foreach (var key in keys)
        {
            Assert.True(
                string.IsNullOrEmpty(configuration[$"PowerBI:{key}"]),
                $"PowerBI:{key} must be empty in the committed base config -- real values belong only in environment variables / App Service settings.");
        }
    }
}
