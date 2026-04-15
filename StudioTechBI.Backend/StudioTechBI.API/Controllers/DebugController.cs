using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.Models;

namespace StudioTechBI.API.Controllers;

[ApiController]
[Route("api/debug")]
public sealed class DebugController : ControllerBase
{
    private readonly IOptions<GoogleAuthOptions> _googleAuthOptions;
    private readonly IWebHostEnvironment _env;

    public DebugController(IOptions<GoogleAuthOptions> googleAuthOptions, IWebHostEnvironment env)
    {
        _googleAuthOptions = googleAuthOptions;
        _env = env;
    }

    /// <summary>
    /// Development-only: returns effective Google OAuth config (no secrets).
    /// </summary>
    [HttpGet("google-config")]
    public IActionResult GetGoogleConfig()
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var opt = _googleAuthOptions.Value;
        return Ok(new
        {
            clientId = opt.ClientId,
            redirectUri = opt.RedirectUri,
            isConfigured = opt.IsAuthorizationConfigured
        });
    }
}

