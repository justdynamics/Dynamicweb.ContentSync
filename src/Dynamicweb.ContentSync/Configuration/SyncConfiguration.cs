namespace Dynamicweb.ContentSync.Configuration;

public record SyncConfiguration
{
    /// <summary>
    /// Top-level folder relative to Files/System. Subfolders are managed automatically:
    /// serializeRoot/ (YAML files), upload/ (zip imports), download/ (zip exports).
    /// </summary>
    public required string OutputDirectory { get; init; }
    public string LogLevel { get; init; } = "info";
    public bool DryRun { get; init; } = false;
    public ConflictStrategy ConflictStrategy { get; init; } = ConflictStrategy.SourceWins;
    public required List<PredicateDefinition> Predicates { get; init; }

    /// <summary>Subfolder for YAML serialization files (scheduled tasks read/write here).</summary>
    public string SerializeRoot => Path.Combine(OutputDirectory, "serializeRoot");

    /// <summary>Subfolder for zip files uploaded for import.</summary>
    public string UploadDir => Path.Combine(OutputDirectory, "upload");

    /// <summary>Subfolder for zip files produced by ad-hoc serialize.</summary>
    public string DownloadDir => Path.Combine(OutputDirectory, "download");
}
