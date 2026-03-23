# Phase 13: Provider Foundation + SqlTableProvider Proof - Research

**Researched:** 2026-03-23
**Domain:** Pluggable provider architecture + SQL table serialization via DW Deployment tool patterns
**Confidence:** HIGH

## Summary

Phase 13 establishes a pluggable provider architecture (`ISerializationProvider` interface + `ProviderRegistry`) and proves `SqlTableProvider` works by round-tripping the `EcomOrderFlow` SQL table to YAML and back. This is greenfield code that sits alongside the existing v1.3 codebase without modifying it.

The research thoroughly analyzed DW10's own Deployment tool source code -- specifically `SqlDataItemReader`, `SqlDataItemWriter`, `LocalDeploymentProvider`, and `XmlDataGroupRepository` -- which are the canonical patterns for SQL table reading, identity resolution, MERGE upsert, and DataGroup metadata access. Our SqlTableProvider must follow these patterns closely because they handle edge cases (identity columns, composite PKs, NULL normalization) that are non-obvious and already battle-tested.

The primary risk areas are: (1) accessing DW's `XmlDataGroupRepository` from our app context (it is `internal` in the DW assembly, so we need to use `DataGroupRepository` abstract class or construct `XmlDataGroupRepository` directly via the documented path pattern), (2) correctly implementing the `CommandBuilder`-based MERGE statement pattern from `SqlDataItemWriter`, and (3) handling NULL vs empty string fidelity in YAML round-trips.

**Primary recommendation:** Follow DW's Deployment tool patterns exactly for SQL operations. Use `Dynamicweb.Data.Database` static API with `CommandBuilder` for all SQL access. Use `XmlDataGroupRepository` via the public `DataGroupRepository` abstract base to read DataGroup XML metadata. Implement identity resolution matching `SqlDataItemReader`'s `IdentitySeparator = "$$"` and alphabetically-ordered key columns pattern.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Flat per-table folder layout: `_sql/{TableName}/{RowName}.yml` -- one folder per table, one YAML file per row named by NameColumn value
- **D-02:** Include `_meta.yml` in each table folder with column definitions, name column, PK info -- self-documenting, helps debugging
- **D-03:** Provider-prefixed output directories (`_content/`, `_sql/`, `_settings/`, `_schema/`) prevent collision between providers
- **D-04:** Interface + base class design: `ISerializationProvider` interface with `SerializationProviderBase` abstract class providing shared YAML helpers and logging setup
- **D-05:** Interface includes `ValidatePredicate()` for config-time validation (e.g., reject SqlTable predicate missing dataGroupId)
- **D-06:** Interface methods: Serialize, Deserialize, DryRun, ValidatePredicate, ProviderType (string), DisplayName (string)
- **D-07:** Use DW's own DataGroup/schema API (`XmlDataGroupRepository`) to read metadata at runtime -- DataGroup XMLs live at `Files/System/Deployment/DataGroups/`
- **D-08:** Predicate config references a DataGroup ID (e.g., `"dataGroupId": "Settings_Ecommerce_Orders_060_OrderFlows"`), provider resolves table metadata via DW runtime APIs
- **D-09:** Spike needed: verify XmlDataGroupRepository is accessible from our app context and returns DataItemType with Table/NameColumn/CompareColumns
- **D-10:** Tables WITH NameColumn: match rows by NameColumn value on deserialize (upsert)
- **D-11:** Tables WITHOUT NameColumn: use composite primary key from `sp_pkeys`, joined with `$$` separator, alphabetically ordered -- follows DW Deployment tool pattern exactly
- **D-12:** SQL upsert via `MERGE` statement with identity column handling (SET IDENTITY_INSERT ON when identity is part of PK)
- **D-13:** Two-pass import strategy: first pass inserts/updates, second pass resolves FK references -- follows DW Deployment tool's ParentId hierarchy pattern
- **D-14:** Use DW's `Dynamicweb.Data.Database` API for all SQL operations -- respect connection pooling and transaction scope, no raw SqlConnection
- **D-15:** Change detection via MD5 checksum of CompareColumns (or all non-AutoId columns if CompareColumns not specified) -- same as DW Deployment tool

### Claude's Discretion
- Exact SerializationProviderBase implementation (shared helpers, logging setup)
- ProviderRegistry implementation (static dictionary vs DI)
- FlatFileStore internal implementation (file naming sanitization, path limits)
- YAML serializer configuration for SQL row data
- Test structure and assertion patterns

### Deferred Ideas (OUT OF SCOPE)
- Orchestrator wiring (dispatching predicates to providers) -- Phase 14
- ContentProvider adapter wrapping existing serializers -- Phase 14
- FK dependency ordering across multiple tables -- Phase 15
- ServiceCache invalidation after SQL writes -- Phase 15
- DW admin UI changes (menu relocation, log viewer) -- Phase 16
- DataGroup auto-discovery (enumerate all available groups) -- Future
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PROV-01 | Pluggable ISerializationProvider interface with Serialize/Deserialize/DryRun contract | DW Deployment tool patterns studied (DataItemProvider/Reader/Writer hierarchy); interface design documented in ARCHITECTURE.md with code examples; D-04/D-05/D-06 lock the contract |
| PROV-02 | Provider registry mapping data type strings to provider instances | Simple static dictionary pattern; DW's `DataItemProvider.CreateInstance(dataItemType)` pattern studied in LocalDeploymentProvider |
| SQL-01 | SqlTableProvider serializes any SQL table to YAML using DataGroup XML metadata (Table, NameColumn, CompareColumns) | `XmlDataGroupRepository.GetById()` returns `DataGroup` with `DataItemTypes` collection; `DataItemType.ProviderParameters` contains Table/NameColumn/CompareColumns; `SqlDataItemReader` pattern for row reading verified; `DatabaseSchemaHelper` for schema introspection |
| SQL-02 | Identity resolution matches rows by NameColumn with CompareColumns fallback for empty NameColumn tables | `SqlDataItemReader.GenerateId()` uses `$$` separator on alphabetically-ordered PK columns; NameColumn matching via `item.Name` construction pattern; `FilterAutoIdPropertiesForComparison` excludes AutoId columns from checksum |
| SQL-04 | Structured result objects report rows added/updated/skipped/failed per table | Existing `DeserializeResult` record pattern (Created/Updated/Skipped/Failed/Errors) provides the template |
| SQL-05 | Source-wins conflict strategy: YAML rows overwrite matched target rows | `SqlDataItemWriter.BuildMergeCommand()` implements MERGE with WHEN MATCHED THEN UPDATE -- source-wins by construction |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Dynamicweb | 10.23.9 | DW10 platform APIs (Database, DataGroup, SystemInformation) | Already referenced; provides Database.CreateDataReader, Database.ExecuteNonQuery, CommandBuilder |
| YamlDotNet | 13.7.1 | YAML serialization/deserialization for SQL row data | Already referenced; reuse YamlConfiguration factory |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit | 2.9.3 | Unit test framework | Already in test project; test new provider classes |
| Moq | 4.20.72 | Mocking framework | Already in test project; mock Database access for unit tests |

### No New Dependencies
Phase 13 requires NO new NuGet packages. Everything needed is already available:
- `Dynamicweb.Data.Database` (static class) for SQL operations
- `Dynamicweb.Data.CommandBuilder` for parameterized queries
- `Dynamicweb.Deployment.XmlDataGroupRepository` for DataGroup XML parsing
- `Dynamicweb.Deployment.DataGroup` / `DataItemType` for metadata model
- `YamlDotNet` for YAML I/O
- `System.Data` for IDataReader, DataTable

## Architecture Patterns

### Recommended Project Structure
```
src/Dynamicweb.ContentSync/
  Providers/                                # NEW - Provider architecture
    ISerializationProvider.cs               # Core interface (D-04, D-06)
    SerializationProviderBase.cs            # Abstract base with shared YAML/logging
    ProviderRegistry.cs                     # Static dictionary mapping type strings to instances
    SqlTable/                               # SqlTableProvider
      SqlTableProvider.cs                   # Serialize/Deserialize/DryRun for SQL tables
      DataGroupMetadataReader.cs            # Wraps XmlDataGroupRepository, extracts table metadata
      SqlTableReader.cs                     # SELECT * via Database.CreateDataReader
      SqlTableWriter.cs                     # MERGE via Database.ExecuteNonQuery + CommandBuilder
      FlatFileStore.cs                      # Write/read per-row YAML files in _sql/{Table}/
      SqlTableResult.cs                     # Structured result (extends DeserializeResult pattern)
  Serialization/                            # EXISTING - unchanged
  Configuration/                            # EXISTING - unchanged (config extension is Phase 14)
  Infrastructure/                           # EXISTING - unchanged
  Models/                                   # EXISTING + small additions
    SqlRowData.cs                           # NEW - Dictionary<string, object?> wrapper for YAML
    TableMetadata.cs                        # NEW - Parsed DataGroup metadata DTO
```

### Pattern 1: Provider Interface with Base Class (D-04, D-06)
**What:** `ISerializationProvider` defines Serialize/Deserialize/DryRun/ValidatePredicate. `SerializationProviderBase` provides shared YAML serializer/deserializer instances and logging helpers.
**When to use:** All provider implementations.
**Example:**
```csharp
// Source: ARCHITECTURE.md + CONTEXT.md D-04/D-06
public interface ISerializationProvider
{
    string ProviderType { get; }        // "Content", "SqlTable", etc.
    string DisplayName { get; }

    SerializeResult Serialize(ProviderPredicateDefinition predicate, string outputRoot, Action<string>? log = null);
    DeserializeResult Deserialize(ProviderPredicateDefinition predicate, string inputRoot, Action<string>? log = null, bool isDryRun = false);
    ValidationResult ValidatePredicate(ProviderPredicateDefinition predicate);
}
```

### Pattern 2: Metadata-Driven SQL Operations (D-07, D-08)
**What:** SqlTableProvider reads DataGroup XML to discover table name, NameColumn, CompareColumns, then generates SQL dynamically. One provider class handles all SQL tables.
**When to use:** Every SQL table serialization.
**Example:**
```csharp
// Source: DW10 XmlDataGroupRepository + SqlDataItemProvider patterns
// Access DataGroup XML at runtime:
var repositoryPath = SystemInformation.MapPath("/Files/System/Deployment/DataGroups");
var repository = new XmlDataGroupRepository(repositoryPath);
var group = repository.GetById("Settings_Ecommerce_Orders_060_OrderFlows");
// group.DataItemTypes contains DataItemType entries with ProviderParameters:
//   Table = "EcomOrderFlow", NameColumn = "OrderFlowName", CompareColumns = ""
```

### Pattern 3: MERGE Upsert with Identity Handling (D-12)
**What:** Follow `SqlDataItemWriter.BuildMergeCommand()` pattern exactly -- MERGE statement with conditional IDENTITY_INSERT when identity column is part of PK.
**When to use:** All SQL table deserialization writes.
**Example:**
```csharp
// Source: DW10 SqlDataItemWriter.cs lines 88-152
// Key pattern: check if any identity column is also a key column
var enableIdentityInsert = identityColumns.Any(ic => keyColumns.Contains(ic));

var cb = new CommandBuilder();
if (enableIdentityInsert)
    cb.Add($"SET IDENTITY_INSERT [{table}] ON;");
cb.Add($"MERGE [{table}] AS target");
cb.Add("USING (SELECT ");
// ... parameterized source values via CommandBuilder {0} placeholders
cb.Add(") AS source (columns...)");
cb.Add("ON (target.[key] = source.[key])");
cb.Add("WHEN MATCHED THEN UPDATE SET ...");
cb.Add("WHEN NOT MATCHED THEN INSERT (...) VALUES (...)");
if (enableIdentityInsert)
    cb.Add($"SET IDENTITY_INSERT [{table}] OFF;");
Database.ExecuteNonQuery(cb);
```

### Pattern 4: Identity Resolution with $$ Separator (D-10, D-11)
**What:** Follow `SqlDataItemReader.GenerateId()` exactly. PK columns sorted alphabetically, values joined with `$$`. NameColumn used for human-readable file names.
**When to use:** All SQL table serialization for file naming and deserialization for row matching.
**Example:**
```csharp
// Source: DW10 SqlDataItemReader.cs lines 158-161
private const string IdentitySeparator = "$$";
// Key columns from sp_pkeys, sorted alphabetically:
var id = string.Join(IdentitySeparator,
    keyColumns.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
              .Select(key => Converter.ToString(data[key]).Trim()));
// For file naming: use NameColumn value if available, else use PK-based id
```

### Pattern 5: Checksum for Change Detection (D-15)
**What:** MD5 checksum of CompareColumns (or all non-AutoId columns). Follow `SqlDataItemReader.CalculateChecksum()` and `DataItemReader.CalculateChecksum()`.
**When to use:** Detecting whether a row needs updating during deserialization.
**Example:**
```csharp
// Source: DW10 SqlDataItemReader.cs lines 50-68 + DataItemReader.cs lines 68-79
// If CompareColumns specified: filter to only those columns
// If CompareColumns empty: filter out *AutoId columns
// Then: sort by key, concatenate "KEY=VALUE|KEY=VALUE", MD5 hash
var sorted = new SortedDictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
var s = string.Join("|", sorted.Select(pair => $"{pair.Key.ToUpperInvariant()}={pair.Value}"));
var hash = StringHelper.Md5HashToString(s);
```

### Anti-Patterns to Avoid
- **Raw SqlConnection:** Never open SqlConnection directly. Use `Database.CreateDataReader(CommandBuilder)` and `Database.ExecuteNonQuery(CommandBuilder)`.
- **One provider class per table:** One SqlTableProvider handles all tables via DataGroup metadata.
- **Numeric PK as match key for cross-environment sync:** Use NameColumn or composite PK string, never numeric identity values as canonical identifiers.
- **Omitting NULL values in YAML:** Must emit explicit `null` (`~`) for SQL NULL. Configure YamlDotNet to preserve null keys.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| SQL parameterized queries | String concatenation with SqlCommand | `Dynamicweb.Data.CommandBuilder` | Handles parameter formatting, SQL injection prevention, collection parameters |
| SQL connection management | `new SqlConnection(connStr)` | `Database.CreateDataReader()` / `Database.ExecuteNonQuery()` | Manages pooling, logging, transaction scope |
| DataGroup XML parsing | Custom XML parser for DataGroup files | `XmlDataGroupRepository.GetById(id)` | Already handles ID resolution, file I/O, deserialization to DataGroup/DataItemType model |
| Schema introspection (columns, PKs, identity) | Raw `INFORMATION_SCHEMA` queries | `DatabaseSchemaHelper.GetSchemaTable()`, `GetKeyColumns()`, `GetIdentityColumns()`, `GetColumns()` | Handles edge cases, uses `sp_pkeys` for reliable PK detection |
| YAML serialization | Custom string building | `YamlConfiguration.BuildSerializer()` / `BuildDeserializer()` | CamelCase naming, ForceStringScalarEmitter, null handling already configured |
| File name sanitization | Regex replacement | Existing `SanitizeFolderName()` pattern from FileSystemStore | Handles all invalid path characters, empty names |
| MD5 checksum | Custom hash implementation | `StringHelper.Md5HashToString()` from Dynamicweb.Core | Already used by DW Deployment tool for same purpose |

**Key insight:** DW10's Deployment tool already solves every SQL operation we need. Our SqlTableProvider should follow its patterns exactly, using the same DW APIs (Database, CommandBuilder, DatabaseSchemaHelper, XmlDataGroupRepository). We are NOT building new SQL infrastructure -- we are composing existing DW APIs into a YAML serialization pipeline.

## Common Pitfalls

### Pitfall 1: XmlDataGroupRepository is Internal
**What goes wrong:** `XmlDataGroupRepository` is declared `internal` in Dynamicweb.Core. Attempting to instantiate it directly from our assembly fails at compile time.
**Why it happens:** DW marks implementation classes internal, exposing only the `DataGroupRepository` abstract base and `LocalDeploymentProvider` as public entry points.
**How to avoid:** Use `LocalDeploymentProvider.DataGroupRepository` property accessor if running in DW context. Alternatively, construct via the public `DataGroupRepository` abstract type. If direct construction is needed, the constructor takes an absolute path (`new XmlDataGroupRepository(absolutePath)`) -- check if the type is accessible via the `Dynamicweb` NuGet package's public API surface. **This is the D-09 spike: verify accessibility at runtime before planning implementation details.**
**Warning signs:** Compile error "XmlDataGroupRepository is inaccessible due to its protection level."

### Pitfall 2: NULL vs Empty String YAML Round-Trip
**What goes wrong:** SQL NULL becomes empty string or omitted key in YAML. On deserialize, empty string is written to a column that should be NULL. DW ecommerce uses `IS NULL` checks where NULL means "inherit from parent."
**Why it happens:** YamlDotNet with `DefaultValuesHandling.OmitNull` omits null keys entirely. On deserialize, missing keys are left at their C# default (null for reference types, 0 for value types) which may not map back to SQL NULL correctly.
**How to avoid:** For SQL table YAML, do NOT omit null keys. Configure a separate serializer that always emits null keys as `~`. On deserialize, explicitly map `null` YAML values to `DBNull.Value` in CommandBuilder parameters.
**Warning signs:** Columns that were NULL become empty strings after round-trip. Application behavior changes for "inherit" settings.

### Pitfall 3: DatabaseSchemaHelper is Internal
**What goes wrong:** Like `XmlDataGroupRepository`, `DatabaseSchemaHelper` is `internal` to Dynamicweb.Core.
**Why it happens:** DW encapsulates schema introspection as internal implementation detail.
**How to avoid:** Implement equivalent queries directly using `Database.CreateDataReader()`:
- PK columns: `Database.CreateDataReader("sp_pkeys @table_name = '{table}'")` or use `CommandBuilder`
- Schema/columns: `Database.CreateDataReader("SELECT * FROM [{table}] WHERE 1=0")` then `reader.GetSchemaTable()`
- Identity columns: `SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table}' AND COLUMNPROPERTY(OBJECT_ID(TABLE_SCHEMA + '.' + TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 1`
**Warning signs:** Compile error on `DatabaseSchemaHelper` references.

### Pitfall 4: CommandBuilder Parameter Limits
**What goes wrong:** SQL Server has a 2,100 parameter limit. Tables with many columns (30+) multiplied by batch size can exceed this in MERGE statements.
**Why it happens:** Each column value in a MERGE statement is a separate parameter. A table with 30 columns has 30 parameters per MERGE. This is fine for single-row MERGE (DW's pattern) but could be an issue if batching is attempted.
**How to avoid:** Follow DW's pattern of one MERGE per row (see `SqlDataItemWriter.WriteItems` loop). Do not batch MERGE statements. The CommandBuilder has a built-in `SqlClientParameterLimit = 2100` constant it respects.

### Pitfall 5: File Name Collisions from NameColumn Values
**What goes wrong:** Two rows in EcomOrderFlow could have the same `OrderFlowName` value (unlikely but not schema-constrained). The second row's YAML file overwrites the first.
**Why it happens:** NameColumn is not necessarily unique. It is a display name, not a primary key.
**How to avoid:** Use NameColumn value for the file name but append PK suffix when duplicates are detected. Pattern: `{NameColumnValue}.yml` normally, `{NameColumnValue} [{PKValue}].yml` on collision. Same dedup pattern as `FileSystemStore.GetPageFolderName()`.

### Pitfall 6: DryRun Mode Must Not Execute SQL Writes
**What goes wrong:** DryRun mode accidentally executes MERGE statements, modifying the database when user expected a preview.
**Why it happens:** DryRun check is missing from a code path, or the flag is not threaded through properly.
**How to avoid:** Check `isDryRun` at the top of `SqlTableWriter.WriteRow()` before any CommandBuilder execution. In dry-run mode, build the MERGE command for logging/preview but never call `Database.ExecuteNonQuery()`. Follow existing `ContentDeserializer` pattern where `_isDryRun` is checked before every DW write call.

## Code Examples

### DataGroup XML Structure (EcommerceCountries example)
```xml
<!-- Source: DW10 test DataGroups/EcommerceCountries.xml -->
<DataGroup Id="EcommerceCountries" Name="Ecommerce Countries" ParentId="Ecommerce">
  <DataItemTypes>
    <DataItemType Id="EcommerceCountry" Name="Countries"
                  ProviderTypeName="Dynamicweb.Deployment.DataItemProviders.Sql.SqlDataItemProvider">
      <ProviderParameters>
        <Parameter Name="Table" Value="EcomCountries" />
        <!-- NameColumn and CompareColumns may be absent or empty -->
      </ProviderParameters>
    </DataItemType>
  </DataItemTypes>
</DataGroup>
```

### DataGroup Model (from DW source)
```csharp
// Source: DW10 Dynamicweb.Deployment.DataGroup
public class DataGroup {
    public string Id { get; set; }
    public string Name { get; set; }
    public string ParentId { get; set; }
    public ICollection<DataItemType> DataItemTypes { get; set; }
    public ICollection<string> ServiceCaches { get; set; }
}

// Source: DW10 Dynamicweb.Deployment.DataItemType
public class DataItemType {
    public string Id { get; set; }
    public string Name { get; set; }
    public string ProviderTypeName { get; set; }
    public IDictionary<string, string> ProviderParameters { get; set; }
    // ProviderParameters["Table"], ["NameColumn"], ["CompareColumns"]
}
```

### Reading Table Rows via Database API
```csharp
// Source: DW10 SqlDataItemReader.ReadItems() pattern
var cb = new CommandBuilder();
cb.Add($"SELECT * FROM [{tableName}]");
using var reader = Database.CreateDataReader(cb);
while (reader.Read())
{
    var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    for (int i = 0; i < reader.FieldCount; i++)
    {
        var value = reader.GetValue(i);
        row[reader.GetName(i)] = value == DBNull.Value ? null : value;
    }
    yield return row;
}
```

### MERGE Statement Pattern
```csharp
// Source: DW10 SqlDataItemWriter.BuildMergeCommand() -- simplified
var cb = new CommandBuilder();
if (enableIdentityInsert)
    cb.Add($"SET IDENTITY_INSERT [{table}] ON;");

cb.Add($"MERGE [{table}] AS target");
cb.Add("USING (SELECT ");
int count = 0;
foreach (var col in itemColumns)
{
    if (count > 0) cb.Add(",");
    cb.Add("{0}", row[col]);  // CommandBuilder parameterizes via {0}
    count++;
}
cb.Add($") AS source ({string.Join(",", itemColumns.Select(c => $"[{c}]"))})");
cb.Add($"ON ({string.Join(" AND ", keyColumns.Select(c => $"target.[{c}] = source.[{c}]"))})");

if (updateColumns.Count > 0)
{
    cb.Add("WHEN MATCHED THEN UPDATE SET");
    cb.Add(string.Join(",", updateColumns.Select(c => $"[{c}] = source.[{c}]")));
}

cb.Add($"WHEN NOT MATCHED THEN INSERT ({string.Join(",", insertColumns.Select(c => $"[{c}]"))})");
cb.Add($"VALUES ({string.Join(",", insertColumns.Select(c => $"source.[{c}]"))});");

if (enableIdentityInsert)
    cb.Add($"SET IDENTITY_INSERT [{table}] OFF;");

Database.ExecuteNonQuery(cb);
```

### FlatFileStore YAML Output Example
```yaml
# _sql/EcomOrderFlow/_meta.yml
table: EcomOrderFlow
nameColumn: OrderFlowName
compareColumns: ""
keyColumns:
  - OrderFlowId
identityColumns:
  - OrderFlowId
columns:
  - OrderFlowId
  - OrderFlowName
  - OrderFlowDescription
  - OrderFlowAutoId

# _sql/EcomOrderFlow/Checkout.yml
orderFlowId: "1"
orderFlowName: "Checkout"
orderFlowDescription: "Standard checkout flow"
orderFlowAutoId: ~
```

### ProviderRegistry Pattern
```csharp
// Claude's discretion: simple static dictionary
public class ProviderRegistry
{
    private readonly Dictionary<string, ISerializationProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public void Register(ISerializationProvider provider)
        => _providers[provider.ProviderType] = provider;

    public ISerializationProvider GetProvider(string providerType)
        => _providers.TryGetValue(providerType, out var provider)
            ? provider
            : throw new InvalidOperationException($"No provider registered for type '{providerType}'");

    public bool HasProvider(string providerType)
        => _providers.ContainsKey(providerType);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Direct ContentSerializer usage | Provider interface + adapter pattern | Phase 13+ | All new data types use provider interface; content stays wrapped |
| No SQL table support | SqlTableProvider driven by DataGroup XML | Phase 13 | 74 SQL tables can be serialized with one provider |
| Hardcoded per-table providers | Metadata-driven single provider | Architecture decision | One class handles all SQL DataItemTypes |

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 + Moq 4.20.72 |
| Config file | `tests/Dynamicweb.ContentSync.Tests/Dynamicweb.ContentSync.Tests.csproj` |
| Quick run command | `dotnet test tests/Dynamicweb.ContentSync.Tests -x --no-build` |
| Full suite command | `dotnet test tests/Dynamicweb.ContentSync.Tests` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PROV-01 | ISerializationProvider defines Serialize/Deserialize/DryRun/ValidatePredicate | unit | `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "FullyQualifiedName~ProviderInterface" -x` | No - Wave 0 |
| PROV-02 | ProviderRegistry resolves correct provider by type string | unit | `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "FullyQualifiedName~ProviderRegistry" -x` | No - Wave 0 |
| SQL-01 | SqlTableProvider serializes EcomOrderFlow rows to YAML files | unit (mock Database) | `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "FullyQualifiedName~SqlTableSerialize" -x` | No - Wave 0 |
| SQL-02 | Identity resolution by NameColumn, fallback to composite PK with $$ separator | unit | `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "FullyQualifiedName~IdentityResolution" -x` | No - Wave 0 |
| SQL-04 | Structured result reports Created/Updated/Skipped/Failed counts | unit | `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "FullyQualifiedName~SqlTableResult" -x` | No - Wave 0 |
| SQL-05 | MERGE statement overwrites matched rows (source-wins) | unit (verify CommandBuilder output) | `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "FullyQualifiedName~MergeStatement" -x` | No - Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Dynamicweb.ContentSync.Tests --no-build -x`
- **Per wave merge:** `dotnet test tests/Dynamicweb.ContentSync.Tests`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Dynamicweb.ContentSync.Tests/Providers/ProviderRegistryTests.cs` -- covers PROV-01, PROV-02
- [ ] `tests/Dynamicweb.ContentSync.Tests/Providers/SqlTable/SqlTableReaderTests.cs` -- covers SQL-01 (mock Database.CreateDataReader)
- [ ] `tests/Dynamicweb.ContentSync.Tests/Providers/SqlTable/SqlTableWriterTests.cs` -- covers SQL-05 (verify MERGE output)
- [ ] `tests/Dynamicweb.ContentSync.Tests/Providers/SqlTable/IdentityResolutionTests.cs` -- covers SQL-02
- [ ] `tests/Dynamicweb.ContentSync.Tests/Providers/SqlTable/FlatFileStoreTests.cs` -- covers SQL-01 YAML I/O
- [ ] `tests/Dynamicweb.ContentSync.Tests/Providers/SqlTable/SqlTableResultTests.cs` -- covers SQL-04
- [ ] `tests/Dynamicweb.ContentSync.Tests/Providers/SqlTable/DataGroupMetadataReaderTests.cs` -- covers D-07/D-08

**Note on testability:** `Dynamicweb.Data.Database` is a static class and cannot be easily mocked. Tests for SqlTableReader/Writer should either: (a) abstract Database access behind a thin interface injectable for testing, or (b) test the CommandBuilder output generation separately from execution. Option (a) is preferred -- create an `ISqlExecutor` interface with `IDataReader ExecuteReader(CommandBuilder)` and `int ExecuteNonQuery(CommandBuilder)` that wraps the static Database calls in production but can be mocked in tests.

## Open Questions

1. **XmlDataGroupRepository accessibility (D-09 Spike)**
   - What we know: `XmlDataGroupRepository` is `internal` in the DW assembly. The public type is `DataGroupRepository` (abstract). `LocalDeploymentProvider` exposes a public `DataGroupRepository` property.
   - What's unclear: Can we construct `XmlDataGroupRepository` directly? Is it visible via the Dynamicweb NuGet package? Or must we use reflection / an alternative approach?
   - Recommendation: First task should be a spike -- try to access `XmlDataGroupRepository` or parse the XML manually. Fallback: parse the XML files directly (they are simple XML, not complex) using `System.Xml.Linq`.

2. **DatabaseSchemaHelper accessibility**
   - What we know: `DatabaseSchemaHelper` is `internal` in Dynamicweb.Core. Its methods use `sp_pkeys` for PK detection and `GetSchemaTable()` for column/identity info.
   - What's unclear: Is it accessible via the NuGet package?
   - Recommendation: Implement equivalent queries via `Database.CreateDataReader()` directly. The SQL queries are straightforward (`sp_pkeys`, `INFORMATION_SCHEMA`, `COLUMNPROPERTY`).

3. **EcomOrderFlow table schema**
   - What we know: It is the proof target -- smallest, simplest table with NameColumn.
   - What's unclear: Exact column list, whether it has identity columns, whether it has FK relationships (EcomOrderStates references it, but does EcomOrderFlow itself have FKs?).
   - Recommendation: First implementation task should query the actual table schema at runtime. This is not a blocker -- SqlTableProvider reads schema dynamically.

## Sources

### Primary (HIGH confidence)
- DW10 source: `SqlDataItemReader.cs` -- Row reading, identity resolution, checksum (direct file analysis)
- DW10 source: `SqlDataItemWriter.cs` -- MERGE upsert, identity column handling (direct file analysis)
- DW10 source: `LocalDeploymentProvider.cs` -- Two-pass import, DataGroup hierarchy, ServiceCache invalidation (direct file analysis)
- DW10 source: `XmlDataGroupRepository.cs` -- DataGroup XML I/O, GetById/GetAll (direct file analysis)
- DW10 source: `DataGroup.cs`, `DataItemType.cs` -- Model classes (direct file analysis)
- DW10 source: `DatabaseSchemaHelper.cs` -- Schema introspection patterns (direct file analysis)
- DW10 source: `Database.cs` -- SQL API surface (CreateDataReader, ExecuteNonQuery, CommandBuilder) (direct file analysis)
- DW10 source: `SqlDataItemProvider.cs` -- Table/NameColumn/CompareColumns properties (direct file analysis)
- DataGroup XML examples: `EcommerceCountries.xml`, `Ecommerce.xml` (direct file analysis)
- Existing codebase: `ContentSerializer.cs`, `ContentDeserializer.cs`, `DeserializeResult.cs`, `FileSystemStore.cs`, `YamlConfiguration.cs`, `ConfigLoader.cs`, `PredicateDefinition.cs` (direct file analysis)

### Secondary (MEDIUM confidence)
- `.planning/research/ARCHITECTURE.md` -- Provider interface design, disk layout, build order (authored from codebase analysis)
- `.planning/research/PITFALLS.md` -- FK ordering, identity handling, NULL round-trip pitfalls (authored from codebase analysis + SQL Server knowledge)

### Tertiary (LOW confidence)
- None -- all findings verified against DW10 source code

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - all libraries already in project, no new dependencies
- Architecture: HIGH - patterns directly derived from DW10 Deployment tool source code
- Pitfalls: HIGH - based on direct analysis of DW10 source code edge cases and existing v1.3 learnings

**Research date:** 2026-03-23
**Valid until:** 2026-04-23 (stable domain -- DW10 Deployment tool patterns unlikely to change)
