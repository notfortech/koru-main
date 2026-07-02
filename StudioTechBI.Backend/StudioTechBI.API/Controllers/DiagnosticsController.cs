using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;

namespace StudioTechBI.API.Controllers;

/// <summary>
/// Operational diagnostics endpoint for internal use.
/// Returns AgentHost connectivity and configuration status — never exposes secrets.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DiagnosticsController : ControllerBase
{
    private readonly IAgentHostClient _agentHostClient;
    private readonly AgentHostOptions _opts;
    private readonly IWebHostEnvironment _env;

    public DiagnosticsController(
        IAgentHostClient agentHostClient,
        IOptions<AgentHostOptions> options,
        IWebHostEnvironment env)
    {
        _agentHostClient = agentHostClient;
        _opts = options.Value;
        _env = env;
    }

    /// <summary>
    /// GET /api/diagnostics/agenthost
    /// Returns AgentHost configuration and live health status. ApiKey is never included.
    /// </summary>
    [HttpGet("agenthost")]
    public async Task<IActionResult> GetAgentHostDiagnosticsAsync(CancellationToken cancellationToken)
    {
        bool canConnect;
        string healthStatus;

        try
        {
            canConnect = await _agentHostClient.CheckHealthAsync(cancellationToken);
            healthStatus = canConnect ? "Healthy" : "Unhealthy";
        }
        catch
        {
            canConnect = false;
            healthStatus = "Unknown";
        }

        return Ok(new
        {
            baseUrl = MaskUrl(_opts.BaseUrl),
            healthStatus,
            canConnect,
            circuitBreakerEnabled = _opts.EnableCircuitBreaker,
            circuitFailureThreshold = _opts.CircuitFailureThreshold,
            circuitBreakDurationSeconds = _opts.CircuitBreakDurationSeconds,
            retryCount = _opts.RetryCount,
            timeoutSeconds = _opts.TimeoutSeconds,
            environment = _env.EnvironmentName,
            applicationVersion = typeof(DiagnosticsController).Assembly
                .GetName().Version?.ToString() ?? "unknown"
        });
    }

    private static string MaskUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "(not configured)";
        try
        {
            var uri = new Uri(url);
            return $"{uri.Scheme}://{uri.Host}";
        }
        catch
        {
            return "(invalid url)";
        }
    }
}
