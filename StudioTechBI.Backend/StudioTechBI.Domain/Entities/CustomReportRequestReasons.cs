namespace StudioTechBI.Domain.Entities;

/// <summary>Distinguishes why a CustomReportRequest was filed, so staff can triage correctly
/// (build a template vs. check for a systemic AI/network issue) without opening every ticket.</summary>
public static class CustomReportRequestReasons
{
    /// <summary>The relevant match/verify call succeeded, but nothing cleared the confidence bar --
    /// the normal, healthy "we searched and found nothing close enough" case.</summary>
    public const string NoConfidentMatch = "NoConfidentMatch";

    /// <summary>The call itself failed (network error, AI service unreachable, timeout) rather than
    /// completing and returning a low-confidence result -- a different problem needing a different
    /// staff response (retry / check for an outage), not template-building work.</summary>
    public const string GenerationError = "GenerationError";
}
