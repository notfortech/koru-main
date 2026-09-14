using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace StudioTechBI.API.Tests.TestHelpers;

internal static class ControllerTestHelpers
{
    /// <summary>Builds an authenticated ClaimsPrincipal the way the real JWT pipeline would (Email +
    /// optional client_code claim), for wiring into a controller's ControllerContext in tests.</summary>
    public static ClaimsPrincipal AuthenticatedUser(string email, string? clientCode = null, string? nameIdentifier = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, email),
            new(ClaimTypes.NameIdentifier, nameIdentifier ?? Guid.NewGuid().ToString()),
        };
        if (clientCode != null)
            claims.Add(new Claim("client_code", clientCode));

        var identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    public static void SetUser(this ControllerBase controller, ClaimsPrincipal user)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = user }
        };
    }
}
