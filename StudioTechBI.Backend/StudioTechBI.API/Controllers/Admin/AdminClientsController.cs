using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.API.Authorization;
using StudioTechBI.API.Controllers;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers.Admin;

[ApiController]
[Route("api/admin/clients")]
[Authorize(Roles = "Admin,SuperAdmin,OperationsAdmin,SupportAdmin")]
public class AdminClientsController : BaseApiController
{
    private readonly IClientService _clientService;

    public AdminClientsController(IClientService clientService)
    {
        _clientService = clientService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ClientCreateDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var client = await _clientService.CreateAsync(dto, cancellationToken);
            return CreatedAtAction(
                nameof(GetById),
                new { id = client.ClientId },
                ApiResponse<ClientDto>.SuccessResponse(client, "Client created."));
        }
        catch (InvalidOperationException)
        {
            return HandleResult(ApiResponse<ClientDto>.ErrorResponse("Client request could not be completed."));
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var list = await _clientService.GetAllAsync(cancellationToken);
        return HandleResult(ApiResponse<IReadOnlyList<ClientDto>>.SuccessResponse(list, "Clients retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var client = await _clientService.GetByIdAsync(id, cancellationToken);
        if (client == null)
            return NotFound();
        return HandleResult(ApiResponse<ClientDto>.SuccessResponse(client));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ClientUpdateDto dto, CancellationToken cancellationToken)
    {
        var client = await _clientService.UpdateAsync(id, dto, cancellationToken);
        if (client == null)
            return NotFound();
        return HandleResult(ApiResponse<ClientDto>.SuccessResponse(client, "Client updated."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _clientService.DeleteAsync(id, cancellationToken);
        if (!deleted)
            return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Phase 1 onboarding: creates the client (and blob folders), activates the portal user, assigns them to this client,
    /// and ensures the Client role. Does not configure Power BI report/workspace/dataset IDs (phase 2).
    /// Pass <c>userId</c> or <c>userEmail</c> of an existing portal user (e.g. after self-registration).
    /// </summary>
    [HttpPost("provision-phase-one")]
    public async Task<IActionResult> ProvisionPhaseOne([FromBody] ClientPhaseOneProvisionDto dto, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid request", errors));
            }

            var result = await _clientService.ProvisionPhaseOneAsync(dto, cancellationToken);
            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Client.ClientId },
                ApiResponse<ClientPhaseOneProvisionResultDto>.SuccessResponse(result, "Phase 1 provisioning completed."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<ClientPhaseOneProvisionResultDto>.ErrorResponse(ex.Message));
        }
    }

    /// <summary>Assign a user to this client. The user will then see and refresh only this client's reports (folder key = ClientCode).</summary>
    [HttpPost("{clientId:guid}/users/{userId:guid}")]
    public async Task<IActionResult> AssignUserToClient(Guid clientId, Guid userId, CancellationToken cancellationToken)
    {
        var assigned = await _clientService.AssignUserToClientAsync(userId, clientId, cancellationToken);
        if (!assigned)
            return NotFound();
        return NoContent();
    }
}
