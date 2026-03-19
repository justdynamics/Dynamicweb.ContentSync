namespace Dynamicweb.ContentSync.Configuration;

public record SyncConfiguration
{
    public required string OutputDirectory { get; init; }
    public string LogLevel { get; init; } = "info";
    public required List<PredicateDefinition> Predicates { get; init; }
}
