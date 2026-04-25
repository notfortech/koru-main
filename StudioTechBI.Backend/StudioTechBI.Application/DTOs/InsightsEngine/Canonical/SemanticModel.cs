namespace StudioTechBI.Application.DTOs.InsightsEngine.Canonical;

public sealed class SemanticModel
{
    public List<SemanticTable> Tables { get; set; } = new();
    public List<SemanticRelationship> Relationships { get; set; } = new();
    public List<SemanticMeasure> Measures { get; set; } = new();
}

public sealed class SemanticTable
{
    public string Name { get; set; } = string.Empty;
    public List<SemanticColumn> Columns { get; set; } = new();
}

public sealed class SemanticColumn
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class SemanticRelationship
{
    public string From { get; set; } = string.Empty; // Table.Column
    public string To { get; set; } = string.Empty;   // Table.Column
    public string Cardinality { get; set; } = string.Empty; // OneToMany, ManyToOne, ...
}

public sealed class SemanticMeasure
{
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
}

