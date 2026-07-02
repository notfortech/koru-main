using Microsoft.Extensions.Diagnostics.HealthChecks;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Clients;

/// <summary>
/// ASP.NET Core IHealthCheck that pings STBI-AgentHost via IAgentHostClient.CheckHealthAsync.
/// Registered in AgentHostIntegrationExtensions so the /health endpoint includes AgentHost status.
/// </summary>
public sealed class AgentHostHealthCheck : IHealthCheck
{
    private readonly IAgentHostClient _client;

    public AgentHostHealthCheck(IAgentHostClient client)
    {
        _client = client;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var healthy = await _client.CheckHealthAsync(cancellationToken);
            return healthy
                ? HealthCheckResult.Healthy("AgentHost is reachable.")
                : HealthCheckResult.Degraded("AgentHost returned a non-success status code.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("AgentHost health check threw an exception.", ex);
        }
    }
}
