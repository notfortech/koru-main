namespace StudioTechBI.API.Services.Domain;

/// <summary>Domain.com.au Properties &amp; Locations API (see Domain developer documentation).</summary>
public interface IDomainPropertyLocationService
{
    Task<object> GetAddressLocatorsAsync(string searchText, CancellationToken cancellationToken = default);

    Task<object> GetDisclaimersAsync(CancellationToken cancellationToken = default);

    Task<object> GetDisclaimersByProductAsync(string product, CancellationToken cancellationToken = default);

    Task<object> GetLocationProfileAsync(int domainLocationId, CancellationToken cancellationToken = default);

    Task<object> GetPropertyByIdAsync(long propertyId, CancellationToken cancellationToken = default);

    Task<object> GetSalesResultsHeadAsync(CancellationToken cancellationToken = default);

    Task<object> GetSalesResultsByCityAsync(string city, CancellationToken cancellationToken = default);

    Task<object> GetSalesResultsListingsAsync(string city, CancellationToken cancellationToken = default);

    Task<object> GetDemographicsAsync(string state, string suburb, string postcode, CancellationToken cancellationToken = default);

    Task<object> GetSuburbPerformanceStatisticsAsync(string state, string suburb, CancellationToken cancellationToken = default);

    Task<object> GetSuburbPerformanceStatisticsByPostcodeAsync(string state, string suburb, string postcode, CancellationToken cancellationToken = default);
}
