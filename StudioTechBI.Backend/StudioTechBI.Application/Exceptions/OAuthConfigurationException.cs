namespace StudioTechBI.Application.Exceptions;

/// <summary>Raised when OAuth client settings are missing or invalid for the current environment.</summary>
public sealed class OAuthConfigurationException : Exception
{
    public OAuthConfigurationException(string message) : base(message)
    {
    }
}
