using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Common;

namespace StudioTechBI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected IActionResult HandleResult<T>(ApiResponse<T> response)
    {
        if (response.Success)
        {
            return Ok(response);
        }

        return BadRequest(response);
    }

    protected IActionResult HandleException(Exception _)
    {
        return StatusCode(500, ApiResponse<object>.ErrorResponse(
            "An error occurred while processing your request.",
            null
        ));
    }
}
