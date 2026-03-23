using Dynamicweb.ContentSync.Models;

namespace Dynamicweb.ContentSync.Providers.SqlTable;

/// <summary>
/// ISerializationProvider implementation for SQL tables.
/// Reads DataGroup XML metadata, reads SQL table rows, resolves row identity,
/// and writes per-row YAML files to _sql/{TableName}/.
/// Serialize is implemented here; Deserialize is deferred to Plan 03.
/// </summary>
public class SqlTableProvider : SerializationProviderBase
{
    private readonly DataGroupMetadataReader _metadataReader;
    private readonly SqlTableReader _tableReader;
    private readonly FlatFileStore _fileStore;

    public override string ProviderType => "SqlTable";
    public override string DisplayName => "SQL Table Provider";

    public SqlTableProvider(DataGroupMetadataReader metadataReader, SqlTableReader tableReader, FlatFileStore fileStore)
    {
        _metadataReader = metadataReader;
        _tableReader = tableReader;
        _fileStore = fileStore;
    }

    public override SerializeResult Serialize(ProviderPredicateDefinition predicate, string outputRoot, Action<string>? log = null)
    {
        var validation = ValidatePredicate(predicate);
        if (!validation.IsValid)
        {
            return new SerializeResult
            {
                Errors = validation.Errors
            };
        }

        var metadata = _metadataReader.GetTableMetadata(predicate.DataGroupId!);
        Log($"Serializing table {metadata.TableName} (DataGroup: {predicate.DataGroupId})", log);

        var rows = _tableReader.ReadAllRows(metadata.TableName).ToList();
        Log($"Read {rows.Count} rows from {metadata.TableName}", log);

        _fileStore.WriteMeta(outputRoot, metadata.TableName, metadata);

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var identity = _tableReader.GenerateRowIdentity(row, metadata);
            _fileStore.WriteRow(outputRoot, metadata.TableName, identity, row, usedNames);
        }

        Log($"Serialized {rows.Count} rows to _sql/{metadata.TableName}/", log);

        return new SerializeResult
        {
            RowsSerialized = rows.Count,
            TableName = metadata.TableName
        };
    }

    public override ProviderDeserializeResult Deserialize(ProviderPredicateDefinition predicate, string inputRoot, Action<string>? log = null, bool isDryRun = false)
    {
        throw new NotImplementedException("Deserialize implemented in Plan 03");
    }

    public override ValidationResult ValidatePredicate(ProviderPredicateDefinition predicate)
    {
        if (!string.Equals(predicate.ProviderType, "SqlTable", StringComparison.OrdinalIgnoreCase))
            return ValidationResult.Failure("Provider type mismatch");

        if (string.IsNullOrEmpty(predicate.DataGroupId))
            return ValidationResult.Failure("DataGroupId is required for SqlTable predicates");

        return ValidationResult.Success();
    }
}
