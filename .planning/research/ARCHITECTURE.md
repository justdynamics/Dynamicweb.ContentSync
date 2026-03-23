# Architecture Research

**Domain:** DynamicWeb database serialization with pluggable provider architecture
**Researched:** 2026-03-23
**Confidence:** HIGH (based on direct codebase analysis and DataGroup XML inspection)

## System Overview

### Current Architecture (v1.3)

```
Admin UI Layer
  ScreenInjector --- TreeNodeProvider --- CommandBase (API)
       |                    |                    |
       +--------------------+--------------------+
                            |
                    SyncConfiguration
                      (Predicates)
                            |
              +-------------+--------------+
              |                            |
       ContentSerializer          ContentDeserializer
              |                            |
       ContentMapper              PermissionMapper
       PermissionMapper           ReferenceResolver
       ReferenceResolver                   |
              |                    +-------+-------+
              |                    |               |
       IContentStore          DW Services     ItemService
       (FileSystemStore)      (Pages,Grids)   (Fields)
              |
         YAML on disk
```

### Target Architecture (v2.0)

```
Admin UI Layer (Settings > Database > Serialize)
  ScreenInjector --- TreeNodeProvider --- CommandBase (API)
  AssetMgmtAction     LogViewerScreen         |
       |                    |                  |
       +--------------------+------------------+
                            |
                   SerializerConfiguration
                   (Providers + Predicates)
                            |
                   SerializerOrchestrator
                            |
            +---------------+----------------+
            |               |                |
     ISerializationProvider |         ISerializationProvider
     (ContentProvider)      |         (SqlTableProvider)
            |               |                |
     ContentMapper    ISerializationProvider  DataGroupReader
     PermissionMapper (SettingsProvider)      SqlColumnMapper
     ReferenceResolver      |                |
            |          SettingsReader    +----+-----+
            |               |           |          |
       IContentStore   IContentStore  ADO.NET   IContentStore
       (FileSystemStore)              (raw SQL)  (FlatFileStore)
            |               |           |          |
         YAML on disk    YAML on disk          YAML on disk
```

### Component Responsibilities

| Component | Responsibility | New/Modified |
|-----------|----------------|--------------|
| `ISerializationProvider` | Interface defining serialize/deserialize contract per data type | **NEW** |
| `SerializationProviderBase` | Base class with shared YAML I/O, logging, config access | **NEW** |
| `ContentProvider` | Wraps existing ContentSerializer/ContentDeserializer behind provider interface | **NEW** (adapter around existing) |
| `SqlTableProvider` | Generic SQL table serialization driven by DataGroup XML metadata | **NEW** |
| `SettingsProvider` | Settings file serialization (KeyPatterns-based) | **NEW** (Wave 4) |
| `SchemaProvider` | DB schema (table structure) serialization | **NEW** (Wave 4) |
| `SerializerOrchestrator` | Replaces direct ContentSerializer usage; iterates providers per config | **NEW** |
| `DataGroupReader` | Parses DataGroup XML files to extract table/column metadata | **NEW** |
| `SqlColumnMapper` | Maps SQL DataReader rows to Dictionary<string, object> for YAML | **NEW** |
| `SerializerConfiguration` | Extended SyncConfiguration with provider-type predicates | **MODIFIED** |
| `ConfigLoader` | Extended to parse new provider config sections | **MODIFIED** |
| `SyncSettingsNodeProvider` | Relocated from Content > Sync to Database > Serialize | **MODIFIED** |
| `SyncSettingsEditScreen` | Updated labels, add provider configuration | **MODIFIED** |
| `LogViewerScreen` | New screen to display log files with guided advice | **NEW** |
| `AssetManagementDeserializeAction` | Injector for deserialize action on zip files in asset management | **NEW** |

## Recommended Project Structure

```
src/DynamicWeb.Serializer/
  Providers/                              # Provider architecture (NEW)
    ISerializationProvider.cs             # Core interface
    SerializationProviderBase.cs          # Shared base class
    ProviderRegistry.cs                   # Discovers and holds provider instances
    Content/                              # ContentProvider (adapter)
      ContentProvider.cs                  # Wraps existing ContentSerializer/Deserializer
    SqlTable/                             # SqlTableProvider
      SqlTableProvider.cs                 # Generic SQL table serialize/deserialize
      DataGroupReader.cs                  # Parses DataGroup XML metadata
      SqlColumnMapper.cs                  # Row-to-dictionary mapping
      FlatFileStore.cs                    # YAML I/O for flat table rows
    Settings/                             # SettingsProvider (Wave 4)
      SettingsProvider.cs
    Schema/                               # SchemaProvider (Wave 4)
      SchemaProvider.cs
  Serialization/                          # Existing (MODIFIED)
    ContentSerializer.cs                  # Unchanged internally, called by ContentProvider
    ContentDeserializer.cs                # Unchanged internally, called by ContentProvider
    ContentMapper.cs                      # Unchanged
    PermissionMapper.cs                   # Unchanged
    ReferenceResolver.cs                  # Unchanged
    DeserializeResult.cs                  # Unchanged
    SerializerOrchestrator.cs             # NEW -- top-level driver
  Configuration/                          # Existing (MODIFIED)
    SerializerConfiguration.cs            # Renamed from SyncConfiguration, extended
    ProviderPredicateDefinition.cs        # NEW -- extends predicates with provider type
    ConfigLoader.cs                       # Modified for new config schema
    ConfigWriter.cs                       # Modified for new config schema
    ConfigPathResolver.cs                 # Unchanged
    ConflictStrategy.cs                   # Unchanged
  Infrastructure/                         # Existing (MODIFIED)
    IContentStore.cs                      # Unchanged
    FileSystemStore.cs                    # Unchanged (content tree I/O)
    YamlConfiguration.cs                  # Unchanged
    ForceStringScalarEmitter.cs           # Unchanged
  Models/                                 # Existing + NEW
    SerializedArea.cs                     # Unchanged
    SerializedPage.cs                     # Unchanged
    SerializedGridRow.cs                  # Unchanged
    SerializedParagraph.cs                # Unchanged
    SerializedPermission.cs               # Unchanged
    SerializedGridColumn.cs               # Unchanged
    SerializedTableRow.cs                 # NEW -- generic SQL row DTO
  AdminUI/                                # Existing (MODIFIED)
    Commands/
      SerializeCommand.cs                 # Renamed, uses orchestrator
      DeserializeCommand.cs               # Renamed, uses orchestrator
      SavePredicateCommand.cs             # Modified for provider predicates
    Screens/
      SerializerSettingsEditScreen.cs     # Renamed
      ProviderPredicateEditScreen.cs      # NEW
      LogViewerScreen.cs                  # NEW
    Injectors/
      AssetManagementDeserializeInjector.cs  # NEW
      ContentSyncPageEditInjector.cs         # Unchanged
    Tree/
      SerializerSettingsNodeProvider.cs   # Renamed, new parent node
    Models/
      ProviderPredicateEditModel.cs       # NEW
  ScheduledTasks/                         # REMOVED (API commands replace them)
```

### Structure Rationale

- **Providers/**: Each provider type in its own subfolder because they have different dependencies (Content API vs ADO.NET vs file I/O). Clean separation prevents SqlTableProvider from pulling in Content namespace.
- **Serialization/ unchanged internally**: ContentSerializer and ContentDeserializer stay intact. ContentProvider is an adapter wrapper, not a rewrite. This minimizes risk since content serialization already works.
- **FlatFileStore inside SqlTable/**: Content uses a mirror-tree layout (nested folders). SQL table rows use a flat layout (one folder per table, one YAML file per row). Different I/O patterns warrant a separate store rather than overloading FileSystemStore.

## Architectural Patterns

### Pattern 1: Provider Interface with Base Class

**What:** An `ISerializationProvider` interface defines the serialize/deserialize contract. A `SerializationProviderBase` provides shared functionality (logging, YAML I/O, config access). Concrete providers implement data-type-specific logic.

**When to use:** Every data group type (Content, SQL tables, Settings, Schema).

**Trade-offs:** Adds one level of indirection over the current direct call. Worth it because 74+ SQL tables need identical handling and ContentProvider is just an adapter.

```csharp
public interface ISerializationProvider
{
    string ProviderType { get; }           // "Content", "SqlTable", "Settings", "Schema"
    string DisplayName { get; }            // For UI/logging

    SerializeResult Serialize(
        ProviderPredicateDefinition predicate,
        string outputRoot,
        Action<string>? log = null);

    DeserializeResult Deserialize(
        ProviderPredicateDefinition predicate,
        string inputRoot,
        Action<string>? log = null,
        bool isDryRun = false);

    /// <summary>
    /// Validates that the predicate is correctly configured for this provider.
    /// Called at config load time.
    /// </summary>
    ValidationResult ValidatePredicate(ProviderPredicateDefinition predicate);
}

public abstract class SerializationProviderBase : ISerializationProvider
{
    public abstract string ProviderType { get; }
    public abstract string DisplayName { get; }

    // Shared YAML serialization/deserialization helpers
    protected ISerializer YamlSerializer { get; }
    protected IDeserializer YamlDeserializer { get; }

    public abstract SerializeResult Serialize(
        ProviderPredicateDefinition predicate,
        string outputRoot,
        Action<string>? log = null);

    public abstract DeserializeResult Deserialize(
        ProviderPredicateDefinition predicate,
        string inputRoot,
        Action<string>? log = null,
        bool isDryRun = false);

    public virtual ValidationResult ValidatePredicate(
        ProviderPredicateDefinition predicate) => ValidationResult.Ok;
}
```

### Pattern 2: Metadata-Driven SqlTableProvider

**What:** Instead of writing a provider per SQL table, one SqlTableProvider handles all 74 SQL-based data items by reading metadata from DataGroup XML files. The XML defines `Table`, `NameColumn`, and `CompareColumns` -- SqlTableProvider uses these to generate SELECT/INSERT/UPDATE statements dynamically.

**When to use:** All DataGroup items with `ProviderTypeName="...SqlDataItemProvider"`.

**Trade-offs:** Very high leverage (one provider covers 74 tables) but requires careful handling of identity columns, foreign key relationships, and tables without a NameColumn. Tables with no NameColumn (like EcomOrderStateRules) need a composite key or primary key matching strategy.

```csharp
public class SqlTableProvider : SerializationProviderBase
{
    public override string ProviderType => "SqlTable";

    public override SerializeResult Serialize(
        ProviderPredicateDefinition predicate,
        string outputRoot,
        Action<string>? log)
    {
        var metadata = DataGroupReader.ReadDataItems(predicate.DataGroupId);
        foreach (var dataItem in metadata)
        {
            var rows = ReadAllRows(dataItem.Table);
            var store = new FlatFileStore(
                Path.Combine(outputRoot, "_sql", dataItem.Table));
            foreach (var row in rows)
            {
                var key = GetRowKey(row, dataItem); // NameColumn or PK
                store.WriteRow(key, row);
            }
        }
        return new SerializeResult { /* counts */ };
    }
}
```

### Pattern 3: Orchestrator Replaces Direct Serializer Calls

**What:** A `SerializerOrchestrator` replaces direct `new ContentSerializer()` calls in commands. It reads the config, resolves which providers handle which predicates, and delegates accordingly.

**When to use:** All entry points (API commands, admin UI actions).

**Trade-offs:** Commands become thinner (just config load + orchestrator call). Slight indirection but enables multi-provider serialization in a single run.

```csharp
public class SerializerOrchestrator
{
    private readonly SerializerConfiguration _config;
    private readonly ProviderRegistry _registry;

    public SerializerOrchestrator(SerializerConfiguration config)
    {
        _config = config;
        _registry = new ProviderRegistry();
    }

    public AggregateResult SerializeAll(
        string outputRoot,
        Action<string>? log = null)
    {
        var aggregate = new AggregateResult();
        foreach (var predicate in _config.Predicates)
        {
            var provider = _registry.GetProvider(predicate.ProviderType);
            var result = provider.Serialize(predicate, outputRoot, log);
            aggregate.Add(predicate.Name, result);
        }
        return aggregate;
    }
}
```

### Pattern 4: ContentProvider as Adapter (Zero Internal Changes)

**What:** ContentProvider wraps the existing ContentSerializer/ContentDeserializer by constructing a SyncConfiguration from the provider predicate and delegating.

**When to use:** Content data type only.

**Trade-offs:** ContentSerializer retains its existing interface and internal behavior. The adapter translates between the new provider interface and the old config-based interface. This means ContentSerializer's return type (void for Serialize) needs a minor enhancement to return counts, or ContentProvider counts by scanning output files.

```csharp
public class ContentProvider : SerializationProviderBase
{
    public override string ProviderType => "Content";
    public override string DisplayName => "Content";

    public override SerializeResult Serialize(
        ProviderPredicateDefinition predicate,
        string outputRoot,
        Action<string>? log)
    {
        // Build backward-compatible SyncConfiguration
        var syncConfig = new SyncConfiguration
        {
            OutputDirectory = Path.Combine(outputRoot, "_content"),
            Predicates = new List<PredicateDefinition>
            {
                new()
                {
                    Name = predicate.Name,
                    Path = predicate.Path ?? "/",
                    AreaId = predicate.AreaId,
                    Excludes = predicate.Excludes ?? new()
                }
            }
        };

        var serializer = new ContentSerializer(syncConfig, log: log);
        serializer.Serialize();

        // Count output files for result
        var fileCount = Directory.GetFiles(
            syncConfig.OutputDirectory, "*.yml",
            SearchOption.AllDirectories).Length;

        return new SerializeResult
        {
            FilesWritten = fileCount
        };
    }
}
```

## Data Flow

### Serialize Flow (v2.0)

```
API Command / Admin Action
    |
SerializerOrchestrator.SerializeAll()
    |
    +-- For each predicate in config:
    |       |
    |       +-- ProviderRegistry.GetProvider(predicate.ProviderType)
    |       |
    |       +-- provider.Serialize(predicate, outputRoot)
    |               |
    |               +-- [ContentProvider]
    |               |       ContentSerializer.Serialize()  (existing code)
    |               |       FileSystemStore.WriteTree()    (mirror-tree layout)
    |               |
    |               +-- [SqlTableProvider]
    |               |       DataGroupReader.ReadDataItems(dataGroupId)
    |               |       For each table: SELECT * -> Dict rows -> YAML files
    |               |       FlatFileStore.WriteRow()       (flat layout)
    |               |
    |               +-- [SettingsProvider]
    |                       Read settings by KeyPatterns -> YAML files
    |
    +-- AggregateResult (counts per provider)
```

### Deserialize Flow (v2.0)

```
API Command / Asset Management Action
    |
SerializerOrchestrator.DeserializeAll()
    |
    +-- For each predicate in config:
    |       |
    |       +-- provider.Deserialize(predicate, inputRoot)
    |               |
    |               +-- [ContentProvider]
    |               |       ContentDeserializer.Deserialize()  (existing code)
    |               |       GUID-based identity matching
    |               |
    |               +-- [SqlTableProvider]
    |               |       For each table: Read YAML -> match by NameColumn/PK
    |               |       INSERT or UPDATE via DW Database API or raw SQL
    |               |       Clear ServiceCaches listed in DataGroup XML
    |               |
    |               +-- [SettingsProvider]
    |                       Read YAML -> write settings files
```

### Configuration Structure (v2.0)

```json
{
  "outputDirectory": "ContentSync",
  "logLevel": "info",
  "dryRun": false,
  "predicates": [
    {
      "name": "Swift Content",
      "providerType": "Content",
      "areaId": 1,
      "path": "/",
      "excludes": []
    },
    {
      "name": "Order Flows",
      "providerType": "SqlTable",
      "dataGroupId": "Settings_Ecommerce_Orders_060_OrderFlows"
    },
    {
      "name": "Countries",
      "providerType": "SqlTable",
      "dataGroupId": "Settings_Ecommerce_Internationalization_010_Countries"
    },
    {
      "name": "Language Settings",
      "providerType": "Settings",
      "keyPatterns": "/Globalsettings/Modules/LanguageManagement"
    }
  ]
}
```

**Backward compatibility:** Predicates without `providerType` default to `"Content"`. Existing v1.x config files continue to work unchanged.

### Key Data Flows

1. **DataGroup XML to SqlTableProvider metadata:** DataGroupReader parses XML at serialize/deserialize time to get table name, name column, compare columns, and service caches. This metadata drives all SQL operations. The XML files live in DW's system folders and are read-only.

2. **SQL row identity resolution:** For tables WITH a NameColumn (e.g., EcomOrderFlow.OrderFlowName), match by name on deserialize. For tables WITHOUT a NameColumn (e.g., EcomOrderStateRules), use a composite key from all non-identity columns or serialize the primary key and match by it. This mirrors DW's own Deployment provider logic.

3. **ServiceCache invalidation:** DataGroup XMLs list service caches to clear after deserialization (e.g., `CountryService`, `VatGroupService`). SqlTableProvider must call these after writing rows to avoid stale in-memory data.

## Disk Layout for SqlTableProvider

SqlTableProvider output differs from ContentProvider's mirror-tree:

```
Files/System/ContentSync/SerializeRoot/
  _content/                           # ContentProvider output (existing mirror-tree)
    Swift/                            # Area name
      area.yml
      Customer Center/
        page.yml
        ...
  _sql/                               # SqlTableProvider output
    EcomOrderFlow/
      _meta.yml                       # Table metadata (columns, name column, PK)
      Checkout.yml                    # One file per row, named by NameColumn
      Quote.yml
      Recurring orders.yml
    EcomOrderStates/
      _meta.yml
      New order.yml
      Processing.yml
      ...
    EcomCountries/
      _meta.yml
      row-1.yml                       # No NameColumn, use PK-based names
      row-2.yml
      ...
    EcomPayments/
      _meta.yml
      Credit Card.yml
      ...
  _settings/                          # SettingsProvider output (Wave 4)
    LanguageManagement/
      settings.yml
  _schema/                            # SchemaProvider output (Wave 4)
    EcomProducts/
      schema.yml
```

**Why prefixed subdirectories:** The `_content`, `_sql`, `_settings`, `_schema` prefixes prevent collision between provider outputs and make it clear which provider owns which folder. The underscore prefix ensures they sort first in directory listings.

## Integration Points

### Existing Code Preserved (Adapter Pattern)

The critical integration decision: **ContentSerializer and ContentDeserializer remain unchanged internally.** The new `ContentProvider` is a thin adapter that constructs a SyncConfiguration from the provider predicate and delegates to the existing classes.

### New Integration Points

| Integration Point | What Changes | Risk |
|---|---|---|
| `SerializerConfiguration` replaces `SyncConfiguration` | Config model extended with providerType per predicate. SyncConfiguration kept internally for ContentProvider backward compat. | LOW -- additive change |
| `ConfigLoader` extended | New predicate fields parsed. Old format still loads (providerType defaults to "Content"). | LOW -- backward compatible |
| API commands use `SerializerOrchestrator` | ContentSyncSerializeCommand/DeserializeCommand delegate to orchestrator instead of creating ContentSerializer directly. | LOW -- same outcome, different path |
| Tree node relocation | SyncSettingsNodeProvider changes parent from `Content_Settings` to `Database_Settings` (new DW section). ID changes from `ContentSync_Settings` to `Serializer_Settings`. | MEDIUM -- must verify DW has Database settings section |
| Asset management injector | New EditScreenInjector on file detail screen to add "Deserialize" action for .zip files. | MEDIUM -- need to verify DW's asset management extension points |
| SQL access for SqlTableProvider | Must use DW's `Dynamicweb.Data.Database` API (not raw SqlConnection) to respect connection pooling and transaction scope. | MEDIUM -- verify API exists and supports raw queries |
| ServiceCache clearing | After SQL table deserialization, must invalidate caches listed in DataGroup XML. Need to verify DW provides a cache-clear API. | MEDIUM -- verify mechanism |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| Orchestrator to Providers | Direct method call via ISerializationProvider | Provider instances created by ProviderRegistry |
| ContentProvider to existing Serialization/ | Direct instantiation of ContentSerializer/Deserializer | Adapter pattern, no interface needed |
| SqlTableProvider to DataGroupReader | DataGroupReader returns typed metadata objects | Read-only XML parsing, no DW dependencies |
| SqlTableProvider to Database | Via Dynamicweb.Data.Database static API | Must verify exact API surface for SELECT/INSERT/UPDATE |
| Admin UI to Orchestrator | Commands create orchestrator, pass config | Same pattern as current commands |

## Anti-Patterns

### Anti-Pattern 1: Rewriting ContentSerializer to Fit Provider Interface

**What people do:** Refactor ContentSerializer internals to accept provider-style parameters.
**Why it's wrong:** ContentSerializer has 5+ days of battle-tested DW API workarounds (grid row item creation, layout template re-application, property item persistence). Touching it risks regressions.
**Do this instead:** ContentProvider is an adapter wrapper. ContentSerializer stays exactly as-is internally.

### Anti-Pattern 2: One Provider Instance Per SQL Table

**What people do:** Create 74 provider classes (EcomOrderFlowProvider, EcomCountriesProvider, etc.).
**Why it's wrong:** All 74 tables follow the same SELECT/INSERT/UPDATE pattern. The only variation is table name, name column, and compare columns -- which are already defined in DataGroup XMLs.
**Do this instead:** One SqlTableProvider class, driven by DataGroup XML metadata. The predicate config references a `dataGroupId` that points to the XML.

### Anti-Pattern 3: Breaking Config Backward Compatibility

**What people do:** Replace the config format, requiring all existing configs to be rewritten.
**Why it's wrong:** Existing v1.x deployments have working configs. Breaking them forces a manual migration step.
**Do this instead:** Predicates without `providerType` default to `"Content"`. Existing config files work unchanged.

### Anti-Pattern 4: Raw SQL Without DW Database API

**What people do:** Open SqlConnection directly for table reads/writes.
**Why it's wrong:** Bypasses DW's connection pooling, logging, and potentially transaction management.
**Do this instead:** Use `Dynamicweb.Data.Database.CreateDataReader()` or equivalent DW database API. Verify exact methods during implementation.

### Anti-Pattern 5: Mixing Provider Output Directories

**What people do:** Write SQL table YAML files alongside content YAML files in the same directory tree.
**Why it's wrong:** Content uses mirror-tree layout with area/page folders. SQL table output uses flat per-table folders. Mixing them creates ambiguity about which provider owns which files and makes cleanup impossible.
**Do this instead:** Provider-prefixed subdirectories (`_content/`, `_sql/`, `_settings/`, `_schema/`).

## Suggested Build Order

The build order follows dependency chains and risk ordering:

### Wave 1: Foundation (Provider Architecture + SqlTableProvider Proof)

**Dependencies:** None (greenfield code)
**Goal:** Establish the provider interface and prove SqlTableProvider works on one table.

1. `ISerializationProvider` + `SerializationProviderBase` -- Define the contract
2. `ProviderRegistry` -- Simple dictionary mapping providerType strings to instances
3. `ProviderPredicateDefinition` -- Extended predicate model with providerType, dataGroupId fields
4. `DataGroupReader` -- Parse DataGroup XML files to extract table metadata
5. `SqlColumnMapper` -- Map DataReader rows to Dictionary<string, object>
6. `FlatFileStore` -- Write/read per-row YAML files
7. `SqlTableProvider.Serialize()` -- SELECT all rows, write YAML
8. `SqlTableProvider.Deserialize()` -- Read YAML, INSERT/UPDATE via DW Database API
9. **Prove on EcomOrderFlow** -- Smallest, simplest table with a clear NameColumn

### Wave 2: Content Migration (Adapter)

**Dependencies:** Wave 1 (ISerializationProvider interface)
**Goal:** Wrap existing content code in provider interface without changing internals.

1. `ContentProvider` -- Adapter wrapping ContentSerializer + ContentDeserializer
2. `SerializerOrchestrator` -- Iterate predicates, dispatch to providers
3. `ConfigLoader` changes -- Parse providerType, default to "Content"
4. API commands updated -- Use SerializerOrchestrator instead of direct ContentSerializer
5. **Verify existing content round-trip still works** -- Regression test

### Wave 3: Ecommerce Settings (SqlTableProvider at Scale)

**Dependencies:** Wave 1 (SqlTableProvider), Wave 2 (Orchestrator)
**Goal:** Serialize/deserialize the ~15 ecommerce settings tables.

1. Add predicates for OrderFlows, OrderStates, Payment, Shipping, Countries, Currencies, VAT
2. Handle foreign key ordering (e.g., Countries before CountryText, OrderFlow before OrderStates)
3. ServiceCache invalidation -- Parse ServiceCaches from DataGroup XML, clear after deserialize
4. Handle tables without NameColumn -- Composite key or PK matching strategy

### Wave 4: Settings + Schema Providers

**Dependencies:** Wave 1 (provider interface)
**Goal:** Add SettingsProvider (KeyPatterns-based) and SchemaProvider.

1. `SettingsProvider` -- Read/write DW settings by KeyPatterns
2. `SchemaProvider` -- Serialize table schema (column definitions)
3. Verify on LanguageManagement settings and EcomProducts schema

### Wave 5: Remaining SQL Tables

**Dependencies:** Wave 3 (SqlTableProvider proven at scale)
**Goal:** Users, Marketing, PIM, Apps tables (~30 tables).

1. Add predicates for remaining tables
2. Handle any table-specific quirks (some tables may have unusual schemas)

### Wave 6: Admin UI Changes

**Dependencies:** Wave 2 (Orchestrator working, config format finalized)
**Goal:** Menu relocation, log viewer, asset management action.

1. Rename: SyncSettingsNodeProvider to SerializerSettingsNodeProvider
2. Relocate tree node from Content > Sync to Database > Serialize
3. Provider predicate edit screen -- dataGroupId picker, providerType selector
4. LogViewerScreen -- Read log files, parse structured entries, show guided advice
5. AssetManagementDeserializeInjector -- Deserialize action on .zip files
6. Remove ScheduledTasks/ -- Delete SerializeScheduledTask and DeserializeScheduledTask

### Wave Ordering Rationale

- **Wave 1 first** because ISerializationProvider is the foundation everything else builds on, and SqlTableProvider covers 74/124 data groups (highest leverage). Proving it on one table de-risks the entire architecture.
- **Wave 2 before Wave 3** because the orchestrator must work before adding more providers. Content migration is low-risk (adapter pattern) and validates the orchestrator flow.
- **Wave 3 before Wave 5** because ecommerce tables have the most foreign key complexity. Solving ordering and cache invalidation here informs the simpler remaining tables.
- **Wave 6 last** because UI changes are cosmetic and don't affect the serialization pipeline. They depend on the config format being finalized (which happens in Waves 1-2).

### Wave Dependency Graph

```
Wave 1: Provider Interface + SqlTableProvider Proof
    |
    +---> Wave 2: Content Migration (Adapter + Orchestrator)
    |         |
    |         +---> Wave 3: Ecommerce Settings (FK ordering, cache invalidation)
    |         |         |
    |         |         +---> Wave 5: Remaining SQL Tables
    |         |
    |         +---> Wave 6: Admin UI Changes
    |
    +---> Wave 4: Settings + Schema Providers (parallel with Wave 3)
```

## Scaling Considerations

| Concern | 10 tables | 50 tables | 100+ tables |
|---------|-----------|-----------|-------------|
| Serialize time | Seconds | 10-30s | 1-2 min (parallel?) |
| YAML file count | ~100 files | ~1000 files | ~5000+ files |
| Config complexity | Simple | Provider groups helpful | May need "include all SQL" wildcard |
| Deserialize ordering | Trivial | FK chains matter | Topological sort needed |

### Scaling Priorities

1. **First bottleneck: FK ordering at scale.** When deserializing 50+ SQL tables, foreign key dependencies create ordering requirements. Tables with FKs must be deserialized after their referenced tables. For Wave 3, manual ordering in config suffices. For 100+ tables, a topological sort based on FK metadata would be needed.

2. **Second bottleneck: Config verbosity.** With 74 SQL tables, listing each as a separate predicate is unwieldy. Consider a "batch" predicate that references a parent DataGroup (e.g., `Settings_Ecommerce_020_Orders`) and auto-discovers all child DataItemTypes. This is a Wave 5 optimization, not needed initially.

## Sources

- Direct codebase analysis of `C:\VibeCode\Dynamicweb.ContentSync\src\` (all source files)
- DataGroup XML files at `C:\temp\DataGroups\` -- inspected SqlDataItemProvider, SettingsDataItemProvider, SchemaDataItemProvider patterns
- v2.0 pivot document in project memory (`project_v2_pivot.md`)
- PROJECT.md milestone context and wave plan

---
*Architecture research for: DynamicWeb.Serializer v2.0 provider architecture*
*Researched: 2026-03-23*
