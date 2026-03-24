using Dynamicweb.ContentSync.Models;

namespace Dynamicweb.ContentSync.Providers;

/// <summary>
/// Central dispatch: iterates predicates, resolves providers via ProviderRegistry,
/// validates each predicate, and aggregates results across all providers.
/// </summary>
public class SerializerOrchestrator
{
    private readonly ProviderRegistry _registry;

    public SerializerOrchestrator(ProviderRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// Serialize all predicates, optionally filtered by provider type.
    /// Unknown provider types and failed validations are logged and skipped.
    /// </summary>
    public OrchestratorResult SerializeAll(
        List<ProviderPredicateDefinition> predicates,
        string outputRoot,
        Action<string>? log = null,
        string? providerFilter = null)
    {
        var results = new List<SerializeResult>();
        var errors = new List<string>();

        foreach (var predicate in predicates)
        {
            if (providerFilter != null &&
                !string.Equals(predicate.ProviderType, providerFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!_registry.HasProvider(predicate.ProviderType))
            {
                var msg = $"No provider registered for type '{predicate.ProviderType}' (predicate: {predicate.Name})";
                errors.Add(msg);
                log?.Invoke($"WARNING: Skipping predicate '{predicate.Name}' — no provider for type '{predicate.ProviderType}'");
                continue;
            }

            var provider = _registry.GetProvider(predicate.ProviderType);
            var validation = provider.ValidatePredicate(predicate);
            if (!validation.IsValid)
            {
                errors.AddRange(validation.Errors.Select(e => $"{predicate.Name}: {e}"));
                log?.Invoke($"WARNING: Skipping predicate '{predicate.Name}' — validation failed: {string.Join(", ", validation.Errors)}");
                continue;
            }

            var result = provider.Serialize(predicate, outputRoot, log);
            results.Add(result);
        }

        return new OrchestratorResult { SerializeResults = results, Errors = errors };
    }

    /// <summary>
    /// Deserialize all predicates, optionally filtered by provider type.
    /// Unknown provider types and failed validations are logged and skipped.
    /// </summary>
    public OrchestratorResult DeserializeAll(
        List<ProviderPredicateDefinition> predicates,
        string inputRoot,
        Action<string>? log = null,
        bool isDryRun = false,
        string? providerFilter = null)
    {
        var results = new List<ProviderDeserializeResult>();
        var errors = new List<string>();

        foreach (var predicate in predicates)
        {
            if (providerFilter != null &&
                !string.Equals(predicate.ProviderType, providerFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!_registry.HasProvider(predicate.ProviderType))
            {
                var msg = $"No provider registered for type '{predicate.ProviderType}' (predicate: {predicate.Name})";
                errors.Add(msg);
                log?.Invoke($"WARNING: Skipping predicate '{predicate.Name}' — no provider for type '{predicate.ProviderType}'");
                continue;
            }

            var provider = _registry.GetProvider(predicate.ProviderType);
            var validation = provider.ValidatePredicate(predicate);
            if (!validation.IsValid)
            {
                errors.AddRange(validation.Errors.Select(e => $"{predicate.Name}: {e}"));
                log?.Invoke($"WARNING: Skipping predicate '{predicate.Name}' — validation failed: {string.Join(", ", validation.Errors)}");
                continue;
            }

            var result = provider.Deserialize(predicate, inputRoot, log, isDryRun);
            results.Add(result);
        }

        return new OrchestratorResult { DeserializeResults = results, Errors = errors };
    }
}

/// <summary>
/// Aggregated result from orchestrator operations across multiple providers.
/// </summary>
public record OrchestratorResult
{
    public List<SerializeResult> SerializeResults { get; init; } = new();
    public List<ProviderDeserializeResult> DeserializeResults { get; init; } = new();
    public List<string> Errors { get; init; } = new();

    public bool HasErrors =>
        Errors.Count > 0 ||
        SerializeResults.Any(r => r.HasErrors) ||
        DeserializeResults.Any(r => r.HasErrors);

    public string Summary
    {
        get
        {
            var parts = new List<string>();

            if (SerializeResults.Count > 0)
            {
                var totalRows = SerializeResults.Sum(r => r.RowsSerialized);
                parts.Add($"Serialized: {totalRows} rows across {SerializeResults.Count} predicates");
            }

            if (DeserializeResults.Count > 0)
            {
                var created = DeserializeResults.Sum(r => r.Created);
                var updated = DeserializeResults.Sum(r => r.Updated);
                var skipped = DeserializeResults.Sum(r => r.Skipped);
                var failed = DeserializeResults.Sum(r => r.Failed);
                parts.Add($"Deserialized: {created} created, {updated} updated, {skipped} skipped, {failed} failed across {DeserializeResults.Count} predicates");
            }

            if (Errors.Count > 0)
                parts.Add($"Errors: {Errors.Count}");

            return string.Join(". ", parts);
        }
    }
}
