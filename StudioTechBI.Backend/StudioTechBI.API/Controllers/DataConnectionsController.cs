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
    private readonly ILogger<DataConnectionsController> _logger;

    public DataConnectionsController(IDataConnectionService dataConnectionService, ILogger<DataConnectionsController> logger)
    {
        _dataConnectionService = dataConnectionService;
        _logger = logger;
    }

    [HttpPost("~/api/clients/{clientId:guid}/data-connections")]
    public async Task<IActionResult> Register(Guid clientId, [FromBody] RegisterDataConnectionDto body, CancellationToken cancellationToken)
    {
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
        try
        {
            var files = await _dataConnectionService.ListFilesAsync(connectionId, cancellationToken);
            return Ok(ApiResponse<List<FileItem>>.SuccessResponse(files.ToList()));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "List files failed for connection {ConnectionId}.", connectionId);
            return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
        }
    }

    [HttpPost("~/api/data-connections/{connectionId:guid}/import")]
    public async Task<IActionResult> Import(Guid connectionId, [FromBody] ImportConnectorFileRequest body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.FileId))
            return BadRequest(ApiResponse<object>.ErrorResponse("FileId is required."));

        try
        {
            var path = await _dataConnectionService.ImportFileToCreatedBlobAsync(
                connectionId,
                body.FileId,
                body.FileName,
                cancellationToken);
            return Ok(ApiResponse<string>.SuccessResponse(path, "File imported to accounting/created."));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Import failed for connection {ConnectionId}.", connectionId);
            return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Connector not supported for connection {ConnectionId}.", connectionId);
            return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
        }
    }
}
