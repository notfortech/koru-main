using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.API.Authorization;
using StudioTechBI.Application.DTOs.Common;

namespace StudioTechBI.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class UsersController : BaseApiController
{
    /// <summary>
    /// Get current user profile
    /// </summary>
    /// <returns>Current user information</returns>
    /// <response code="200">User profile retrieved successfully</response>
    /// <response code="401">User not authenticated</response>
    [HttpGet("me")]
    [Authorize(Policy = AuthorizationPolicies.AnyAuthenticatedPolicy)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var name = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role);

        var userInfo = new
        {
            Id = userId,
            Email = email,
            Name = name,
            Roles = roles.Select(r => r.Value).ToList()
        };

        return Ok(ApiResponse<object>.SuccessResponse(userInfo, "User profile retrieved successfully"));
    }

    /// <summary>
    /// Admin-only endpoint: Get all users (Admin access required)
    /// </summary>
    /// <returns>List of all users</returns>
    /// <response code="200">Users retrieved successfully</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">User is not an Admin</response>
    [HttpGet("all")]
    [Authorize(Policy = AuthorizationPolicies.AdminPolicy)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public IActionResult GetAllUsers()
    {
        var users = new object[] { };

        return Ok(ApiResponse<object>.SuccessResponse(users, "Users retrieved successfully"));
    }

    /// <summary>
    /// Accountant-only endpoint: Get transaction summary (Accountant or Admin access required)
    /// </summary>
    /// <returns>Transaction summary data</returns>
    /// <response code="200">Transaction summary retrieved successfully</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">User is not an Accountant or Admin</response>
    [HttpGet("transactions/summary")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrAccountantPolicy)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public IActionResult GetTransactionSummary()
    {
        var summary = new
        {
            TotalTransactions = 0,
            TotalAmount = 0m,
            LastUpdate = DateTime.UtcNow
        };

        return Ok(ApiResponse<object>.SuccessResponse(summary, "Transaction summary retrieved successfully"));
    }

    /// <summary>
    /// Client-only endpoint: Get my account details
    /// </summary>
    /// <returns>Client account information</returns>
    /// <response code="200">Client account retrieved successfully</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">User is not a Client</response>
    [HttpGet("account")]
    [Authorize(Policy = AuthorizationPolicies.ClientPolicy)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public IActionResult GetClientAccount()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var account = new
        {
            UserId = userId,
            AccountStatus = "Active",
            CreatedAt = DateTime.UtcNow.AddMonths(-1)
        };

        return Ok(ApiResponse<object>.SuccessResponse(account, "Client account retrieved successfully"));
    }
}
