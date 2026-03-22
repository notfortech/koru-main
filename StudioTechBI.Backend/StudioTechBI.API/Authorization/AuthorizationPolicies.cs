using Microsoft.AspNetCore.Authorization;

namespace StudioTechBI.API.Authorization;

public static class AuthorizationPolicies
{
    public const string AdminPolicy = "AdminOnly";
    public const string AccountantPolicy = "AccountantOnly";
    public const string ClientPolicy = "ClientOnly";
    public const string AdminOrAccountantPolicy = "AdminOrAccountant";
    public const string AnyAuthenticatedPolicy = "AnyAuthenticated";
    public const string PortalAdminPolicy = "PortalAdmin";

    public static void AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AdminPolicy, policy =>
                policy.RequireRole("Admin"));

            options.AddPolicy(AccountantPolicy, policy =>
                policy.RequireRole("Accountant"));

            options.AddPolicy(ClientPolicy, policy =>
                policy.RequireRole("Client"));

            options.AddPolicy(AdminOrAccountantPolicy, policy =>
                policy.RequireRole("Admin", "Accountant"));

            options.AddPolicy(AnyAuthenticatedPolicy, policy =>
                policy.RequireAuthenticatedUser());

            options.AddPolicy(PortalAdminPolicy, policy =>
                policy.RequireRole("Admin", "SuperAdmin", "OperationsAdmin", "SupportAdmin"));
        });
    }
}
