namespace StudioTechBI.Domain.Entities;

/// <summary>
/// Single source of truth for which version of the sign-up data/terms disclaimer is currently in
/// force -- stamped onto User.TermsVersion at registration (see AuthService.RegisterAsync).
/// Bump CurrentVersion whenever the terms text (studiotechbi-ui-main's TermsDialog content)
/// changes meaningfully; a version bump does not retroactively affect existing users' stored
/// TermsVersion or require them to re-consent -- there's no re-consent flow built yet.
/// </summary>
public static class TermsPolicy
{
    public const string CurrentVersion = "2026-08-18";
}
