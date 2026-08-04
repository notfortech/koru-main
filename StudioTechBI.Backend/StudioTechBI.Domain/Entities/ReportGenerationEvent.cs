namespace StudioTechBI.Domain.Entities;

public static class ReportGenerationModes
{
    public const string Deterministic = "Deterministic";
    public const string AiAssisted = "AiAssisted";
}

/// <summary>
/// One row per genuinely new Report Generator report — never per filter re-apply (the wizard
/// re-POSTs the same file on every filter change; only the initial generate call writes a row
/// here, see ReportGeneratorController.GenerateAsync's isRefinement flag). The Report Generator
/// pipeline is otherwise fully ephemeral/stateless (see SavedReport's own remarks) — this is the
/// only place "a report was generated" is durably counted, which is what the client dashboard's
/// Report Stats action reads from.
/// </summary>
public class ReportGenerationEvent : BaseEntity
{
    public Guid ClientId { get; set; }
    public string Mode { get; set; } = ReportGenerationModes.Deterministic;
    public string? TemplateId { get; set; }
    public string? TemplateName { get; set; }
    public string? HtmlTemplateId { get; set; }
    public string? HtmlTemplateName { get; set; }
}
