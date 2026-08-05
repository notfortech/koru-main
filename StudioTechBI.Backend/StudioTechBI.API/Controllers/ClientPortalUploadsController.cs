using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.Constants;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Options;

namespace StudioTechBI.API.Controllers;

[ApiController]
[Route("api/client-portal/uploads")]
[Authorize]
public sealed class ClientPortalUploadsController : ControllerBase
{
    private readonly IBlobStorageService _blobStorage;
    private readonly IClientResolver _clientResolver;
    private readonly IClientByCompanyQuery _clientByCompanyQuery;
    private readonly IClientService _clientService;
    private readonly ILogger<ClientPortalUploadsController> _logger;
    private readonly IOptions<UploadLimitsOptions> _uploadLimits;

    public ClientPortalUploadsController(
        IBlobStorageService blobStorage,
        IClientResolver clientResolver,
        IClientByCompanyQuery clientByCompanyQuery,
        IClientService clientService,
        ILogger<ClientPortalUploadsController> logger,
        IOptions<UploadLimitsOptions> uploadLimits)
    {
        _blobStorage = blobStorage;
        _clientResolver = clientResolver;
        _clientByCompanyQuery = clientByCompanyQuery;
        _clientService = clientService;
        _logger = logger;
        _uploadLimits = uploadLimits;
    }

    private async Task<IReadOnlyList<string>> GetAccessibleClientCodesAsync(CancellationToken ct)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email)) return Array.Empty<string>();

        var fromCompanies = await _clientByCompanyQuery.GetClientsForUserEmailAsync(email, ct);
        var list = fromCompanies
            .Select(c => c.ClientCode ?? "")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var claimCode = User.FindFirstValue("client_code")?.Trim();
        if (!string.IsNullOrEmpty(claimCode) && !list.Any(c => string.Equals(c, claimCode, StringComparison.OrdinalIgnoreCase)))
        {
            var fromClaim = await _clientService.GetByClientCodeOrIdAsync(claimCode, ct);
            if (fromClaim != null)
                list.Add(fromClaim.ClientCode ?? fromClaim.BlobFolderPath ?? claimCode);
        }

        return list;
    }

    private async Task<bool> CanAccessClientAsync(Guid clientId, CancellationToken ct)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email)) return false;

        var fromCompanies = await _clientByCompanyQuery.GetClientsForUserEmailAsync(email, ct);
        if (fromCompanies.Any(c => c.ClientId == clientId))
            return true;

        var claimCode = User.FindFirstValue("client_code")?.Trim();
        if (string.IsNullOrEmpty(claimCode)) return false;

        var fromClaim = await _clientService.GetByClientCodeOrIdAsync(claimCode, ct);
        return fromClaim != null && fromClaim.ClientId == clientId;
    }

    public sealed class UploadAccountingCreatedResponse
    {
        public bool Success { get; set; }
        public string ClientCode { get; set; } = string.Empty;
        public string BlobPath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DateTime UploadedAtUtc { get; set; }
    }

    /// <summary>
    /// Uploads a CSV/XLSX into the client's <c>accounting/created/</c> folder.
    /// Used by the client portal so Insights can read the latest uploaded dataset.
    /// </summary>
    [HttpPost("accounting-created")]
    [RequestSizeLimit(UploadLimits.MaxUploadBytes)]
    public async Task<IActionResult> UploadAccountingCreated(
        [FromForm] IFormFile? file,
        [FromForm] string? clientCode,
        [FromForm] bool useSelectedClient = false,
        CancellationToken ct = default)
    {
        if (file == null || file.Length <= 0)
            return BadRequest(new { success = false, message = "file is required." });

        if (file.Length > _uploadLimits.Value.MaxUploadBytes)
            return BadRequest(new { success = false, message = $"file is too large (max {_uploadLimits.Value.MaxUploadBytes} bytes)." });

        var originalName = (file.FileName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(originalName))
            originalName = "upload.bin";

        var ext = Path.GetExtension(originalName);
        if (!string.Equals(ext, ".csv", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { success = false, message = "Only .csv and .xlsx files are supported." });
        }

        // Resolve target client:
        // - Default: JWT client_code claim
        // - Firm flow: useSelectedClient=true + clientCode must be in accessible list
        var key = User.FindFirstValue("client_code")?.Trim();
        if (useSelectedClient && !string.IsNullOrWhiteSpace(clientCode))
        {
            var accessible = await GetAccessibleClientCodesAsync(ct);
            var normalized = clientCode.Trim();
            if (accessible.Any(c => string.Equals(c, normalized, StringComparison.OrdinalIgnoreCase)))
                key = normalized;
            else
                return StatusCode(403, new { success = false, message = "You do not have access to this client." });
        }

        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { success = false, message = "clientCode is required when the access token has no client_code claim." });

        var client = await _clientResolver.ResolveAsync(key, ct);
        if (client == null)
            return NotFound(new { success = false, message = "Client not found." });

        if (!await CanAccessClientAsync(client.Id, ct))
        {
            _logger.LogWarning("User denied upload to created folder for client {ClientId}.", client.Id);
            return StatusCode(403, new { success = false, message = "You do not have access to this client." });
        }

        var folder = (client.BlobFolderPath ?? client.ClientCode ?? client.Id.ToString()).Trim();
        var safeName = SanitizeFileName(originalName);
        var blobPath = $"{folder}/accounting/created/{Guid.NewGuid():N}_{safeName}";

        var contentType = string.Equals(ext, ".csv", StringComparison.OrdinalIgnoreCase)
            ? "text/csv"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        await using var stream = file.OpenReadStream();
        await _blobStorage.UploadClientBlobAsync(blobPath, stream, contentType, ct);

        return Ok(new UploadAccountingCreatedResponse
        {
            Success = true,
            ClientCode = client.ClientCode ?? key,
            BlobPath = blobPath,
            FileName = safeName,
            UploadedAtUtc = DateTime.UtcNow
        });
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = (fileName ?? "").Trim();
        if (name.Length == 0) return "upload.bin";

        // Remove any path separators
        name = name.Replace("\\", "_").Replace("/", "_");

        // Keep only a safe subset of characters
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-' || c == ' ')
                sb.Append(c);
        }

        var cleaned = sb.ToString().Trim().Replace(' ', '_');
        if (cleaned.Length == 0) cleaned = "upload.bin";

        // Avoid extremely long blob names from user uploads
        if (cleaned.Length > 200)
        {
            var ext = Path.GetExtension(cleaned);
            var baseName = Path.GetFileNameWithoutExtension(cleaned);
            baseName = baseName.Length > 180 ? baseName[..180] : baseName;
            cleaned = baseName + (string.IsNullOrEmpty(ext) ? "" : ext);
        }

        return cleaned;
    }
}

