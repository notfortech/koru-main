namespace StudioTechBI.Application.Constants;

/// <summary>
/// Shared between ReportDesignerController (credit check, before enqueueing) and
/// ReportModelGenerationBackgroundService (credit consumption, after the AI call actually
/// succeeds) so the two can never drift onto different feature names / costs.
/// </summary>
public static class ReportModelGenerationConstants
{
    public const string Feature = "report-model-generation";

    // Flat per-action cost for the local interim ledger (see LocalCreditLedgerService) -- no
    // per-feature pricing exists yet, so every AI-consuming action costs the same 1 credit "for
    // now" until real usage data justifies weighting them differently.
    public const int CreditCost = 1;
}
