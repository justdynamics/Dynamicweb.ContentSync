# Feature Research

**Domain:** CMS database serialization (DynamicWeb full database state to YAML for git-based deployment)
**Researched:** 2026-03-23
**Confidence:** MEDIUM-HIGH (strong patterns from Sitecore Unicorn, Metabase serialization, and existing v1.x codebase; DynamicWeb-specific DataGroup internals verified against API docs but some gaps remain)

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume exist when you claim "full database serialization." Missing these = product feels incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Pluggable provider interface | Unicorn, Metabase, and every mature serializer uses provider abstraction. Without it, adding new data types requires forking core code. | MEDIUM | `ISerializationProvider` with Serialize/Deserialize/DryRun methods. DW's own DataGroup system already categorizes by provider type (SqlDataItemProvider, SettingsDataItemProvider, SchemaDataItemProvider). Mirror that taxonomy. |
| SqlTableProvider for generic SQL tables | 74 of ~124 data groups use SqlDataItemProvider. This is the bulk of the work and the primary value of v2.0. | HIGH | Generic provider reads table metadata from DataGroup XML (Table, NameColumn, CompareColumns), serializes all rows to YAML. Must handle ~74 tables without per-table custom code. |
| Identity resolution via NameColumn | Cross-environment sync requires stable identity. Numeric PKs differ between DBs. DW DataGroups already define NameColumn for each table -- this is the natural key for matching. | MEDIUM | Match on NameColumn for upsert. If NameColumn is null/absent, fall back to composite key from CompareColumns. Content provider already proved this pattern with PageUniqueId (GUID). Metabase uses the same approach: "databases, tables, and fields are referred to by their names." |
| Predicate extension with data type selection | Users need to choose WHAT to serialize (Content, Ecommerce, Settings, etc.), not just which content paths. Current predicates are content-path-only. | MEDIUM | Add `DataType` enum/string to PredicateDefinition. UI dropdown for selecting data group category. Each data type maps to a provider. Follows Unicorn's pattern of multiple named configurations per data type. |
| Settings file serialization (~20 items) | ~20 data groups use SettingsDataItemProvider. These are config files that vary between environments. Users need them serialized alongside SQL data. | LOW | Settings are already files on disk. Provider serializes a manifest of active settings paths and their contents. Simpler than SQL tables -- no identity resolution needed, just file path as key. |
| Schema export (~5 items) | ~5 data groups use SchemaDataItemProvider. Custom table structures that users may have extended. | LOW | Export table structure metadata as YAML. Serves as documentation and enables detecting schema drift between environments. Not for migration -- just reference. |
| Dry-run mode for all providers | Already exists for ContentProvider. Users expect consistency across all provider types. | LOW | Extend existing dry-run pattern to provider interface. Each provider reports what WOULD change (rows to add/update/skip) without writing. Already proven pattern. |
| Error handling with actionable messages | When deserialization fails (missing FK reference, type mismatch, missing group), users need to know exactly what to fix. | MEDIUM | Structured error results per provider. Each result includes: rows processed, rows failed, specific error messages with row identity. Required foundation for log viewer. |
| Source-wins conflict strategy for all providers | Already proven for content. Must extend consistently to SQL tables: source YAML overwrites target rows matched by NameColumn. | LOW | Same pattern as content, applied generically. Unmatched target rows are left alone (not deleted) -- this avoids data loss from partial serialization. |
| Provider registry / discovery | Users and the system need to know which providers are available and which data types they handle. | LOW | Simple dictionary mapping DataType string to ISerializationProvider instance. Populated at startup from registered providers. |

### Differentiators (Competitive Advantage)

Features that set this apart from DW's built-in DataPortability XML export and generic database dump tools.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Log viewer with guided advice | DW's built-in tools give raw logs. A viewer that says "Create missing groups: Account Admin, CSR" or "Table EcomOrderFlow: 3 rows added, 1 updated" is genuinely useful. Transforms raw errors into actionable work items. | MEDIUM | Parse structured log entries. Show per-provider summaries. Highlight actionable items (missing references, failed rows). Requires structured log format from providers. |
| Dependency-aware deserialization ordering | Tables have FK relationships. Deserializing EcomOrderState before EcomOrderFlow fails if states reference flows. Topological sort of table dependencies prevents FK violations automatically. | HIGH | Query `sys.foreign_keys` at runtime or maintain a static dependency map. Topological sort at deserialize time. This is what separates a production tool from one that requires manual ordering. Standard algorithm -- Kahn's or DFS-based toposort on table FK graph. |
| Human-readable YAML over XML | DW's built-in DataPortability export uses XML. YAML is more readable, produces cleaner git diffs, and is easier to hand-edit when needed. Already proven in v1.x. | ALREADY DONE | Extends naturally to SQL table rows. Each row becomes a YAML document keyed by NameColumn value. Git diff shows exactly which fields changed on which named entity. |
| Selective data group serialization via predicates | DW's DataPortability exports everything or nothing. Predicate-based selection lets teams serialize only the data groups they need for their deployment workflow. | LOW | Already have predicate infrastructure from v1.x. Extend with data type dimension. Teams that only care about ecommerce settings don't need to serialize users or marketing data. |
| Asset management deserialize action | Instead of navigating to a settings page, users drop a zip in the file manager and click "Deserialize." Contextual action where the file lives. | MEDIUM | DW Admin API action on file detail page. Detects zip contents, routes to appropriate providers based on YAML structure. More intuitive than the current settings-page approach. |
| DataGroup auto-discovery from DW metadata | Instead of hardcoding table lists, read DataGroup definitions from DW's own XML metadata at runtime. Future-proofs against DW version changes. | MEDIUM | Parse DataGroup XML files to build available provider/table list. Eliminates maintenance burden when DW adds new data groups in future versions. |
| Cross-environment rename detection | When a NameColumn value changes between environments (e.g., "Standard Shipping" renamed to "Standard Delivery"), detect potential matches by CompareColumns similarity. | HIGH | Fuzzy matching using CompareColumns as secondary identity. Log as warning rather than auto-resolve. Useful but complex -- defer unless users report pain. |

### Anti-Features (Commonly Requested, Often Problematic)

Features that seem good but create real problems in a database serializer context.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Bidirectional merge / conflict resolution | "What if both environments changed the same row?" | Exponential complexity. Merge logic for arbitrary SQL rows is unsolvable in the general case. Three-way merge requires tracking base state per row per environment. Sitecore Unicorn explicitly chose source-wins. Metabase uses source-wins (overwrite by entity_id). | Source-wins. Files are truth. Train teams to make changes in source environment, serialize, deploy via git. |
| Incremental / delta sync | "Only sync what changed since last time" | Requires change tracking (triggers, timestamps, CDC). DW tables don't uniformly have modification timestamps. Adds state management (last sync marker per table). Fragile across schema changes. | Full serialize is fast enough for ~74 tables of config data (these are not transactional tables with millions of rows). Run on deploy, not continuously. |
| Real-time change detection via DW Notifications | "Auto-serialize when someone saves in admin" | Notification handlers fire in request context -- serializing to disk during a web request is slow and error-prone. Race conditions with concurrent edits. Coupling to every DW save path. | Manual serialize via API command or CI/CD trigger. Explicit is better than implicit for deployment artifacts. |
| Transactional data serialization (orders, carts, sessions) | "Serialize everything in the database" | Order/cart/session data is high-volume, environment-specific, and time-sensitive. Serializing it makes no sense for deployment workflows. Would produce massive YAML files. | Explicitly exclude transactional tables. The ~74 SqlDataItemProvider tables are configuration/structure data, not transactional data. This is by DW's own design. |
| Per-field merge rules | "Keep target's price but take source's description" | Per-field rules require custom merge config per table. Configuration nightmare across 74 tables. Impossible to maintain across DW upgrades that add/remove columns. | Source-wins for entire rows. If partial updates are needed, do them via post-deploy SQL scripts. |
| Schema migration (ALTER TABLE) | "Automatically migrate target DB schema when columns change" | Schema migration is a solved problem (EF migrations, Flyway, Liquibase). Conflating it with data serialization creates a fragile tool that does two things poorly. | SchemaProvider exports schema as reference YAML. Actual schema changes use DW's own migration system or purpose-built migration tools. |
| File / media serialization | "Serialize images and documents too" | Files are already on disk in the DW file system. They belong in git directly, not round-tripped through a database serializer. Would massively bloat YAML output. | Document that files stay in git. The 24 file-based data groups are explicitly excluded per PROJECT.md. |
| Provider-level parallelism | "Serialize all tables in parallel for speed" | Shared database connections, potential deadlocks during deserialization, FK ordering conflicts. Parallel reads are safe but parallel writes are dangerous. | Sequential provider execution. Serialize is read-only (safe to optimize later). Deserialize MUST be sequential for FK ordering. |

## Feature Dependencies

```
[Provider Interface (ISerializationProvider)]
    |
    +--requires--> [Provider Registry]
    |                  |
    |                  +--enhances--> [DataGroup Auto-Discovery] (v3+)
    |
    +--enables--> [SqlTableProvider]
    |                 |
    |                 +--requires--> [Identity Resolution (NameColumn)]
    |                 +--requires--> [YAML Row Serialization]
    |                 +--enhances--> [Dependency Ordering (FK toposort)]
    |
    +--enables--> [SettingsDataItemProvider]
    |
    +--enables--> [SchemaDataItemProvider]
    |
    +--enables--> [ContentProvider Migration]
    |                 (existing v1.x code wrapped in new interface)

[Predicate Extension (DataType field)]
    +--requires--> [Provider Interface] (predicates reference provider types)
    +--enhances--> [Admin UI Predicate Editor] (dropdown for data type)

[Log Viewer with Guided Advice]
    +--requires--> [Structured Log Format] (providers emit structured results)
    +--requires--> [Provider Interface] (per-provider result summaries)

[Asset Management Deserialize Action]
    +--requires--> [Provider Interface] (runs providers from zip contents)
    +--independent-of--> [Log Viewer] (but benefits from it)

[Scheduled Task Removal]
    +--requires--> [API Commands] (already exist from v1.2)
    +--independent-of--> all new features
```

### Dependency Notes

- **SqlTableProvider requires Identity Resolution:** Without NameColumn matching, the provider cannot determine whether to INSERT or UPDATE rows in the target. This is the core technical challenge of v2.0.
- **Dependency Ordering enhances SqlTableProvider:** Not strictly required for a single table, but multi-table deserialization WILL fail on FK constraints without it. Workaround: temporarily disable FK checks per session (`EXEC sp_msforeachtable "ALTER TABLE ? NOCHECK CONSTRAINT all"` then re-enable). But topological sort is the correct long-term solution.
- **Log Viewer requires Structured Log Format:** Current text-based logging is insufficient. Providers must emit structured results (rows added/updated/skipped with identity and error context) for the log viewer to produce guided advice.
- **Provider Interface is the critical foundation:** Every other feature depends on this being designed correctly. It must accommodate all four provider types. Design it first, validate with ContentProvider migration, then build SqlTableProvider on top.
- **ContentProvider Migration is the design validation:** Existing working code gets wrapped in the new interface. If the interface can't cleanly accommodate the existing content serializer, the design is wrong. Build this immediately after the interface.
- **Predicate Extension depends on Provider Interface:** The DataType field on predicates must reference provider types. Design both together so the enum/string values align.
- **Scheduled Task Removal is independent and low-risk:** API commands already exist from v1.2. Can happen at any point. Pure cleanup.

## MVP Definition

### Launch With (v2.0 Core)

Minimum viable features to claim "full database serialization."

- [ ] Provider interface (`ISerializationProvider`) with Serialize/Deserialize/DryRun contract -- foundation
- [ ] Provider registry mapping DataType strings to provider instances -- routing layer
- [ ] ContentProvider migrated into provider architecture -- validates design, zero new functionality
- [ ] SqlTableProvider handling generic SQL table serialization -- core v2.0 value, covers 74 data groups
- [ ] Identity resolution via NameColumn with CompareColumns fallback -- required for SQL table upsert
- [ ] Predicate extension with DataType field -- users must select what to serialize
- [ ] Ecommerce settings tables (OrderFlows, OrderStates, Payment, Shipping, Countries, Currencies, VAT ~15 tables) -- highest-value data groups, proves SqlTableProvider works
- [ ] Source-wins conflict strategy across all providers -- consistency guarantee
- [ ] Structured result objects from all providers -- foundation for log viewer and error reporting

### Add After Core Validation (v2.x)

Features to add once SqlTableProvider is proven working on ecommerce tables.

- [ ] Dependency-aware deserialization ordering (FK toposort) -- needed for multi-table deserialize reliability
- [ ] SettingsDataItemProvider (~20 settings items) -- simpler than SQL, extends coverage
- [ ] SchemaDataItemProvider (~5 schema items) -- schema reference export
- [ ] Remaining SQL tables: Users, Marketing, PIM, Apps (~30 tables) -- same SqlTableProvider pattern, more coverage
- [ ] Log viewer with guided advice -- UX differentiator, depends on structured logging being solid
- [ ] Asset management deserialize action -- UX convenience for zip-based workflows
- [ ] Scheduled task removal -- cleanup, API commands are the replacement

### Future Consideration (v3+)

Features to defer until v2.x is stable and in production use.

- [ ] DataGroup auto-discovery from DW XML metadata -- future-proofing, nice but not essential when table lists are known
- [ ] Provider-specific predicate filtering (e.g., "only EcomOrderFlows where Active=true") -- per-table row filtering
- [ ] Cross-provider dependency resolution (content referencing ecommerce entities) -- very complex, unclear value
- [ ] Custom provider SDK for third-party extensions -- only if community demand materializes
- [ ] Cross-environment rename detection via CompareColumns -- fuzzy matching, high complexity

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Provider interface (ISerializationProvider) | HIGH | MEDIUM | P1 |
| Provider registry | HIGH | LOW | P1 |
| ContentProvider migration | MEDIUM | LOW | P1 |
| SqlTableProvider (generic SQL) | HIGH | HIGH | P1 |
| Identity resolution (NameColumn) | HIGH | MEDIUM | P1 |
| Predicate extension (DataType) | HIGH | LOW | P1 |
| Ecommerce tables (~15) | HIGH | LOW (once SqlTableProvider works) | P1 |
| Structured result objects | HIGH | MEDIUM | P1 |
| Dependency ordering (FK toposort) | HIGH | HIGH | P2 |
| SettingsDataItemProvider | MEDIUM | LOW | P2 |
| SchemaDataItemProvider | LOW | LOW | P2 |
| Remaining SQL tables (~30) | MEDIUM | LOW | P2 |
| Log viewer with guided advice | MEDIUM | MEDIUM | P2 |
| Asset management deserialize action | MEDIUM | MEDIUM | P2 |
| Scheduled task removal | LOW | LOW | P3 |
| DataGroup auto-discovery | LOW | MEDIUM | P3 |

**Priority key:**
- P1: Must have for v2.0 launch -- proves "full database serialization" claim
- P2: Should have, add in v2.x waves -- expands coverage and UX
- P3: Nice to have, future consideration

## Competitor Feature Analysis

| Feature | DW Built-in DataPortability (XML) | Sitecore Unicorn | Metabase Serialization | Our Approach (DynamicWeb.Serializer) |
|---------|----------------------------------|------------------|----------------------|--------------------------------------|
| Format | XML | Rainbow (custom text) | YAML | YAML -- readable, git-friendly diffs |
| Identity resolution | Primary keys | Item GUIDs (stable across envs) | Entity IDs + name-based for DB objects | NameColumn (natural key from DataGroup metadata) + GUID for content |
| Selective serialization | All or nothing | Predicate-based path inclusion/exclusion with multiple configs | Export with include/exclude flags | Predicate-based with data type dimension |
| Provider architecture | Monolithic export | Pluggable (data store, evaluator, predicate, loader) | Fixed internal serializers | Pluggable per data group type (4 provider types) |
| Conflict resolution | Last write wins | Source-wins default, evaluator-based overrides | Source-wins (overwrite by entity_id) | Source-wins, consistent across all providers |
| Dependency ordering | Not handled (manual table order) | N/A (flat item tree, no FK concerns) | Internal ordering by entity references | FK topological sort for SQL tables |
| Human guidance on errors | Raw error log | Sync console with colored status per item | Basic error reporting | Log viewer with guided advice (differentiator) |
| Git workflow integration | None | Files on disk, CI/CD via API endpoint | CLI export/import, CI/CD pipelines | Files on disk + Management API commands + CI/CD |
| Dry-run mode | No | No (transparent sync shows pending changes) | No | Yes -- reports changes without writing, all providers |
| Schema handling | Exports schema in XML | Serializes template definitions as items | Exports field metadata | SchemaProvider for reference YAML |

## Sources

- [Sitecore Unicorn GitHub](https://github.com/SitecoreUnicorn/Unicorn) -- provider architecture, predicate system, source-wins strategy, multiple configuration support (HIGH confidence)
- [Unicorn Configuration Examples](https://github.com/SitecoreUnicorn/Unicorn/blob/master/src/Unicorn/Standard%20Config%20Files/Unicorn.Configs.Default.example) -- predicate configuration patterns, dependency declarations (HIGH confidence)
- [Metabase Serialization Docs](https://www.metabase.com/docs/latest/installation-and-operation/serialization) -- YAML export, entity ID resolution, name-based DB matching (HIGH confidence)
- [DynamicWeb DataGroup API](https://doc.dynamicweb.com/api/html/fda49b07-764f-35e3-676a-93eb31258b0b.htm) -- DataGroup class: Id, Name, Definition, ItemTypes properties (MEDIUM confidence -- limited detail in public docs)
- [Topological Sort for DB Dependencies](https://dogan-ucar.de/resolving-database-dependencies-using-topological-graph-sorting/) -- FK ordering algorithm pattern (HIGH confidence)
- [SQL Server FK Hierarchy Script](https://www.mssqltips.com/sqlservertip/6179/sql-server-foreign-key-hierarchy-order-and-dependency-list-script/) -- sys.foreign_keys approach for dependency discovery (HIGH confidence)
- [FK Constraint Resolution via Topological Sort](https://www.bigbinary.com/blog/resolve-foreign-key-constraint-conflict-while-copying-data-using-topological-sort) -- practical implementation pattern (HIGH confidence)
- [DW DataPortability NuGet](https://www.nuget.org/packages/Dynamicweb.DataPortability/10.18.15) -- current package version reference (HIGH confidence)
- Existing v1.x codebase (ContentSerializer, ReferenceResolver, PredicateDefinition, ContentPredicate) -- proven patterns to extend (HIGH confidence)
- v2.0 pivot analysis (project_v2_pivot.md) -- wave plan, data group breakdown, SqlTableProvider pattern (HIGH confidence)

---
*Feature research for: DynamicWeb.Serializer v2.0 -- Provider Architecture and Full Database Serialization*
*Researched: 2026-03-23*
