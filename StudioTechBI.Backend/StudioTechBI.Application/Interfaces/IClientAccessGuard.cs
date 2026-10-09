using System.Security.Claims;

namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// Verifies that the authenticated caller is entitled to act on a given Client. Extracted from the
/// identical check already duplicated (correctly) across several controllers
/// (InsightsEngineController.CanAccessClientAsync, ReportsController.GetAccessibleClientCodesAsync,
/// and others) so newly-added authorization checks reuse one proven implementation instead of
/// risking a slightly-wrong copy. Existing controllers that already have their own working copy of
/// this check are not required to switch to this — this exists for call sites that currently have
/// no check at all.
/// </summary>
public interface IClientAccessGuard
{
    /// <summary>True if <paramref name="user"/> (via their email's company-linked clients, or their
    /// JWT client_code claim) is entitled to access <paramref name="clientId"/>.</summary>
    Task<bool> CanAccessClientAsync(ClaimsPrincipal user, Guid clientId, CancellationToken cancellationToken = default);
}
