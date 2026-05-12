using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.API.Services.Domain;

namespace StudioTechBI.API.Controllers;

[ApiController]
[Route("api/propertylocation")]
[Authorize]
public sealed class PropertyLocationController : ControllerBase
{
    private readonly IDomainPropertyLocationService _domain;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PropertyLocationController> _logger;

    public PropertyLocationController(
        IDomainPropertyLocationService domain,
        IWebHostEnvironment environment,
        ILogger<PropertyLocationController> logger)
    {
        _domain = domain;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet("address-locators")]
    public async Task<IActionResult> GetAddressLocators([FromQuery] string searchText, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            () => _domain.GetAddressLocatorsAsync(searchText, cancellationToken),
            "Address locators retrieved.",
            cancellationToken);
    }

    [HttpGet("disclaimers")]
    public async Task<IActionResult> GetDisclaimers(CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            () => _domain.GetDisclaimersAsync(cancellationToken),
            "Disclaimers retrieved.",
            cancellationToken);
    }

    [HttpGet("disclaimers/product/{product}")]
    public async Task<IActionResult> GetDisclaimersByProduct(string product, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            () => _domain.GetDisclaimersByProductAsync(product, cancellationToken),
            "Product disclaimers retrieved.",
            cancellationToken);
    }

    [HttpGet("location-profile/{domainLocationId:int}")]
    public async Task<IActionResult> GetLocationProfile(int domainLocationId, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            () => _domain.GetLocationProfileAsync(domainLocationId, cancellationToken),
            "Location profile retrieved.",
            cancellationToken);
    }

    [HttpGet("property/{id:long}")]
    public async Task<IActionResult> GetProperty(long id, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            () => _domain.GetPropertyByIdAsync(id, cancellationToken),
            "Property retrieved.",
            cancellationToken);
    }

    [HttpGet("sales-results/head", Order = 0)]
    public async Task<IActionResult> GetSalesResultsHead(CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            () => _domain.GetSalesResultsHeadAsync(cancellationToken),
            "Sales results head retrieved.",
            cancellationToken);
    }

    [HttpGet("sales-results/{city}", Order = 1)]
    public async Task<IActionResult> GetSalesResultsByCity(string city, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            () => _domain.GetSalesResultsByCityAsync(city, cancellationToken),
            "Sales results retrieved.",
            cancellationToken);
    }

    [HttpGet("sales-results/{city}/listings", Order = 1)]
    public async Task<IActionResult> GetSalesResultsListings(string city, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            () => _domain.GetSalesResultsListingsAsync(city, cancellationToken),
            "Sales results listings retrieved.",
            cancellationToken);
    }

    [HttpGet("demographics")]
    public async Task<IActionResult> GetDemographics(
        [FromQuery] string state,
        [FromQuery] string suburb,
        [FromQuery] string postcode,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            () => _domain.GetDemographicsAsync(state, suburb, postcode, cancellationToken),
            "Demographics retrieved.",
            cancellationToken);
    }

    [HttpGet("suburb-performance")]
    public async Task<IActionResult> GetSuburbPerformance(
        [FromQuery] string state,
        [FromQuery] string suburb,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            () => _domain.GetSuburbPerformanceStatisticsAsync(state, suburb, cancellationToken),
            "Suburb performance statistics retrieved.",
            cancellationToken);
    }

    [HttpGet("suburb-performance-postcode")]
    public async Task<IActionResult> GetSuburbPerformancePostcode(
        [FromQuery] string state,
        [FromQuery] string suburb,
        [FromQuery] string postcode,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            () => _domain.GetSuburbPerformanceStatisticsByPostcodeAsync(state, suburb, postcode, cancellationToken),
            "Suburb performance statistics retrieved.",
            cancellationToken);
    }

    private async Task<IActionResult> ExecuteAsync(
        Func<Task<object>> action,
        string successMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var data = await action().ConfigureAwait(false);
            return Ok(ApiResponse<object>.SuccessResponse(data, successMessage));
        }
        catch (DomainApiRequestException ex)
        {
            _logger.LogWarning(ex, "Domain property/location API request failed.");
            var status = ex.StatusCode is int sc && sc >= 400 && sc < 600
                ? sc
                : StatusCodes.Status502BadGateway;
            var message = "The property data request could not be completed.";
            var errors = _environment.IsProduction()
                ? null
                : new List<string> { ex.Message };
            return StatusCode(status, ApiResponse<object>.ErrorResponse(message, errors));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Domain integration token or configuration error.");
            var errors = _environment.IsProduction()
                ? null
                : new List<string> { ex.Message };
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "Property data service is not configured or authentication failed.",
                errors));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Domain property/location API.");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.ErrorResponse(
                    "An error occurred while processing your request.",
                    _environment.IsProduction() ? null : new List<string> { ex.Message }));
        }
    }
}
