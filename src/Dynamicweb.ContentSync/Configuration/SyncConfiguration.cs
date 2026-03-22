namespace Dynamicweb.ContentSync.Configuration;

public record SyncConfiguration
{
    public required string OutputDirectory { get; init; }
    public string ExportDirectory { get; init; } = string.Empty;
    public string LogLevel { get; init; } = "info";
    public bool DryRun { get; init; } = false;
    public ConflictStrategy ConflictStrategy { get; init; } = ConflictStrategy.SourceWins;
    public required List<PredicateDefinition> Predicates { get; init; }
}
