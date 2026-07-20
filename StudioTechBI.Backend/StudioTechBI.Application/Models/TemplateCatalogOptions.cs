namespace StudioTechBI.Application.Models;

/// <summary>
/// Where manually-published template datasets live in Power BI. One shared workspace holding
/// every template a designer has published through the admin template library — the rebind flow
/// clones from here into each client's own workspace.
/// </summary>
public class TemplateCatalogOptions
{
    public const string SectionName = "TemplateCatalog";

    public string MasterWorkspaceName { get; set; } = "StudioTechBI Templates";
}
