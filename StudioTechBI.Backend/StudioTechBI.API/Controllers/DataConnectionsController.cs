using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.Connectors;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers;

[ApiController]
[Authorize]
public class DataConnectionsController : ControllerBase
{
    private readonly IDataConnectionService _dataConnectionService;
    private readonly IClientByCompanyQuery _clientByCompanyQuery;
    private readonly IClientService _clientService;
    private readonly ILogger<DataConnectionsController> _logger;

    public DataConnectionsController(
        IDataConnectionService dataConnectionService,
        IClientByCompanyQuery clientByCompanyQuery,
        IClientService clientService,
        ILogger<DataConnectionsController> logger)
    {
        _dataConnectionService = dataConnectionService;
        _clientByCompanyQuery = clientByCompanyQuery;
        _clientService = clientService;
        _logger = logger;
    }

    private async Task<IReadOnlyList<(string Code, Guid Id)>> GetAccessibleClientsAsync(CancellationToken ct)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email)) return Array.Empty<(string, Guid)>();

        var fromCompanies = await _clientByCompanyQuery.GetClientsForUserEmailAsync(email, ct);
        var list = fromCompanies.Select(c => (Code: c.ClientCode ?? "", c.ClientId)).Where(x => !string.IsNullOrEmpty(x.Code)).ToList();

        var claimCode = User.FindFirstValue("client_code");
        if (!string.IsNullOrEmpty(claimCode) && list.All(x => !string.Equals(x.Code, claimCode, StringComparison.OrdinalIgnoreCase)))
        {
            var fromClaim = await _clientService.GetByClientCodeOrIdAsync(claimCode, ct);
            if (fromClaim != null)
                list.Add((fromClaim.ClientCode ?? fromClaim.BlobFolderPath ?? claimCode, fromClaim.ClientId));
        }

        return list;
    }

    private async Task<bool> CanAccessClientAsync(Guid clientId, CancellationToken ct)
    {
        var accessible = await GetAccessibleClientsAsync(ct);
        return accessible.Any(a => a.Id == clientId);
    }

    [HttpGet("~/api/data-connections")]
    public async Task<IActionResult> ListForClient([FromQuery] Guid clientId, CancellationToken cancellationToken)
    {
        if (clientId == Guid.Empty)
            return BadRequest(new { success = false, message = "clientId is required." });

        if (!await CanAccessClientAsync(clientId, cancellationToken))
        {
            _logger.LogWarning("User denied listing data connections for client {ClientId}.", clientId);
            return StatusCode(403, new { success = false, message = "You do not have access to this client." });
        }

        var items = await _dataConnectionService.ListConnectionsForClientAsync(clientId, cancellationToken);
        return Ok(items.Select(x => new { id = x.Id, type = x.Type }).ToList());
    }

    [HttpPost("~/api/clients/{clientId:guid}/data-connections")]
    public async Task<IActionResult> Register(Guid clientId, [FromBody] RegisterDataConnectionDto body, CancellationToken cancellationToken)
    {
        if (!await CanAccessClientAsync(clientId, cancellationToken))
            return StatusCode(403, new { success = false, message = "You do not have access to this client." });

        body.ClientId = clientId;
        try
        {
            var dto = await _dataConnectionService.RegisterConnectionAsync(body, cancellationToken);
            return Ok(ApiResponse<DataConnectionDto>.SuccessResponse(dto, "Data connection registered."));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Register data connection failed for client {ClientId}.", clientId);
            return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
        }
    }

    [HttpGet("~/api/data-connections/{connectionId:guid}/files")]
    public async Task<IActionResult> ListFiles(Guid connectionId, CancellationToken cancellationToken)
    {
        var ownerClientId = await _dataConnectionService.GetConnectionClientIdAsync(connectionId, cancellationToken);
        if (ownerClientId == null)
            return NotFound(new ConnectorFilesResponseDto { Success = false, Files = new List<FileItem>() });
        if (!await CanAccessClientAsync(ownerClientId.Value, cancellationToken))
            return StatusCode(403, new ConnectorFilesResponseDto { Success = false, Files = new List<FileItem>() });

        try
        {
            var files = await _dataConnectionService.ListFilesAsync(connectionId, cancellationToken);
            return Ok(new ConnectorFilesResponseDto { Success = true, Files = files.ToList() });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "List files failed for connection {ConnectionId}.", connectionId);
            return BadRequest(new ConnectorFilesResponseDto { Success = false, Files = new List<FileItem>() });
        }
    }

    [HttpPost("~/api/data-connections/{connectionId:guid}/import")]
    public async Task<IActionResult> Import(Guid connectionId, [FromBody] ImportConnectorFileRequest body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.FileId))
            return BadRequest(new ConnectorImportResponseDto { Success = false, BlobPath = "" });

        var ownerClientId = await _dataConnectionService.GetConnectionClientIdAsync(connectionId, cancellationToken);
        if (ownerClientId == null)
            return NotFound(new ConnectorImportResponseDto { Success = false, BlobPath = "" });
        if (!await CanAccessClientAsync(ownerClientId.Value, cancellationToken))
            return StatusCode(403, new ConnectorImportResponseDto { Success = false, BlobPath = "" });

        try
        {
            var path = await _dataConnectionService.ImportFileToCreatedBlobAsync(
                connectionId,
                body.FileId,
                body.FileName,
                cancellationToken);
            return Ok(new ConnectorImportResponseDto { Success = true, BlobPath = path });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Import failed for connection {ConnectionId}.", connectionId);
            return BadRequest(new ConnectorImportResponseDto { Success = false, BlobPath = "" });
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Connector not supported for connection {ConnectionId}.", connectionId);
            return BadRequest(new ConnectorImportResponseDto { Success = false, BlobPath = "" });
        }
    }
}
