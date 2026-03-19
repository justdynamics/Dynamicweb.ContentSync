namespace Dynamicweb.ContentSync.Configuration;

public record PredicateDefinition
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required int AreaId { get; init; }
    public List<string> Excludes { get; init; } = new();
}
