namespace StudioTechBI.API.Services.Domain;

/// <summary>Domain.com.au resource API returned a non-success response or an unexpected payload.</summary>
public sealed class DomainApiRequestException : Exception
{
    public DomainApiRequestException(string message, int? statusCode, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int? StatusCode { get; }
}
