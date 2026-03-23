# Phase 13: Provider Foundation + SqlTableProvider Proof - Context

**Gathered:** 2026-03-23
**Status:** Ready for planning

<domain>
## Phase Boundary

Establish a pluggable provider architecture (ISerializationProvider interface + registry) and prove SqlTableProvider works by round-tripping a single SQL table (EcomOrderFlow) to YAML and back. This phase does NOT wire up the orchestrator, migrate ContentProvider, or handle FK ordering — those are Phase 14 and 15.

</domain>

<decisions>
## Implementation Decisions

### SQL Table YAML Layout
- **D-01:** Flat per-table folder layout: `_sql/{TableName}/{RowName}.yml` — one folder per table, one YAML file per row named by NameColumn value
- **D-02:** Include `_meta.yml` in each table folder with column definitions, name column, PK info — self-documenting, helps debugging
- **D-03:** Provider-prefixed output directories (`_content/`, `_sql/`, `_settings/`, `_schema/`) prevent collision between providers

### Provider Interface
- **D-04:** Interface + base class design: `ISerializationProvider` interface with `SerializationProviderBase` abstract class providing shared YAML helpers and logging setup
- **D-05:** Interface includes `ValidatePredicate()` for config-time validation (e.g., reject SqlTable predicate missing dataGroupId)
- **D-06:** Interface methods: Serialize, Deserialize, DryRun, ValidatePredicate, ProviderType (string), DisplayName (string)

### DataGroup Metadata Access
- **D-07:** Use DW's own DataGroup/schema API (`XmlDataGroupRepository`) to read metadata at runtime — DataGroup XMLs live at `Files/System/Deployment/DataGroups/`
- **D-08:** Predicate config references a DataGroup ID (e.g., `"dataGroupId": "Settings_Ecommerce_Orders_060_OrderFlows"`), provider resolves table metadata via DW runtime APIs
- **D-09:** Spike needed: verify XmlDataGroupRepository is accessible from our app context and returns DataItemType with Table/NameColumn/CompareColumns

### Identity Resolution (follows DW Deployment tool)
- **D-10:** Tables WITH NameColumn: match rows by NameColumn value on deserialize (upsert)
- **D-11:** Tables WITHOUT NameColumn: use composite primary key from `sp_pkeys`, joined with `$$` separator, alphabetically ordered — follows DW Deployment tool pattern exactly
- **D-12:** SQL upsert via `MERGE` statement with identity column handling (SET IDENTITY_INSERT ON when identity is part of PK)
- **D-13:** Two-pass import strategy: first pass inserts/updates, second pass resolves FK references — follows DW Deployment tool's ParentId hierarchy pattern

### SQL Access
- **D-14:** Use DW's `Dynamicweb.Data.Database` API for all SQL operations — respect connection pooling and transaction scope, no raw SqlConnection
- **D-15:** Change detection via MD5 checksum of CompareColumns (or all non-AutoId columns if CompareColumns not specified) — same as DW Deployment tool

### Claude's Discretion
- Exact SerializationProviderBase implementation (shared helpers, logging setup)
- ProviderRegistry implementation (static dictionary vs DI)
- FlatFileStore internal implementation (file naming sanitization, path limits)
- YAML serializer configuration for SQL row data
- Test structure and assertion patterns

</decisions>

<specifics>
## Specific Ideas

- Follow DW10 Deployment tool patterns as closely as possible — it's battle-tested production code solving the same problems
- Key DW source files to study:
  - `Dynamicweb.Core/Data/DataItemProviders/SqlDataItemProvider.cs` — provider structure
  - `Dynamicweb.Core/Data/DataItemProviders/SqlDataItemReader.cs` — row reading, identity resolution, checksum
  - `Dynamicweb.Core/Data/DataItemProviders/SqlDataItemWriter.cs` — MERGE statement, identity column handling
  - `Dynamicweb.Core/Deployment/LocalDeploymentProvider.cs` — two-pass import, DataGroup hierarchy
  - `Dynamicweb.Core/Deployment/XmlDataGroupRepository.cs` — DataGroup XML access API
- EcomOrderFlow is the proof target — smallest, simplest table with a clear NameColumn

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### DW Deployment Tool (identity resolution, SQL patterns)
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Data\DataItemProviders\SqlDataItemReader.cs` — Row reading, NameColumn identity, composite PK fallback via sp_pkeys, checksum calculation
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Data\DataItemProviders\SqlDataItemWriter.cs` — MERGE statement upsert, identity column handling, two-pass write
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Deployment\LocalDeploymentProvider.cs` — Two-pass import orchestration, DataGroup ParentId hierarchy, FK ordering
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Deployment\XmlDataGroupRepository.cs` — DataGroup XML reading API, GetById/GetAll methods

### DW DataGroup XML Structure
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Deployment.Tests\Files\System\Deployment\DataGroups\` — Example DataGroup XML definitions (EcommerceCountries.xml etc.)

### Existing Codebase (patterns to follow)
- `src\Dynamicweb.ContentSync\Serialization\ContentSerializer.cs` — Serialization orchestration pattern, log callback, predicate iteration
- `src\Dynamicweb.ContentSync\Serialization\ContentDeserializer.cs` — Deserialization pattern, DeserializeResult, dry-run mode
- `src\Dynamicweb.ContentSync\Serialization\DeserializeResult.cs` — Structured result object pattern (Created/Updated/Skipped/Failed/Errors)
- `src\Dynamicweb.ContentSync\Infrastructure\IContentStore.cs` — Existing I/O abstraction pattern
- `src\Dynamicweb.ContentSync\Infrastructure\FileSystemStore.cs` — YAML I/O, file naming, path sanitization
- `src\Dynamicweb.ContentSync\Infrastructure\YamlConfiguration.cs` — YamlDotNet serializer/deserializer factory

### Research
- `.planning\research\ARCHITECTURE.md` — Provider interface design, data flow diagrams, disk layout
- `.planning\research\PITFALLS.md` — FK ordering risks, identity column handling, empty NameColumn

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **YamlConfiguration**: Static factory for YamlDotNet serializer/deserializer with CamelCaseNamingConvention and ForceStringScalarEmitter — reuse for SQL row YAML
- **DeserializeResult**: Structured result pattern (Created/Updated/Skipped/Failed/Errors) — model SqlTableResult after this
- **IContentStore / FileSystemStore**: I/O abstraction pattern — new FlatFileStore follows same pattern for flat per-row files
- **ConfigLoader / PredicateDefinition**: Config parsing pattern — extend for ProviderType and DataGroupId fields

### Established Patterns
- **Log callback**: `Action<string>? log` in constructors — all providers should follow this
- **Dry-run mode**: Boolean flag that switches between reporting-only and actual writes
- **Source-wins**: Existing data cleared/overwritten, never merged
- **Services.Xxx static accessor**: Canonical DW10 pattern for API access
- **Record types for DTOs**: SyncConfiguration, PredicateDefinition, SerializedPage etc. are all records

### Integration Points
- **Provider interface plugs into**: Future orchestrator (Phase 14), management commands (Phase 14), admin UI (Phase 16)
- **Config extension**: PredicateDefinition gains ProviderType and DataGroupId fields — backward compatible (default to "Content")
- **Output directory**: SqlTableProvider writes to `{outputRoot}/_sql/{TableName}/` alongside existing `_content/` from ContentProvider

</code_context>

<deferred>
## Deferred Ideas

- Orchestrator wiring (dispatching predicates to providers) — Phase 14
- ContentProvider adapter wrapping existing serializers — Phase 14
- FK dependency ordering across multiple tables — Phase 15
- ServiceCache invalidation after SQL writes — Phase 15
- DW admin UI changes (menu relocation, log viewer) — Phase 16
- DataGroup auto-discovery (enumerate all available groups) — Future

</deferred>

---

*Phase: 13-provider-foundation-sqltableprovider-proof*
*Context gathered: 2026-03-23*
