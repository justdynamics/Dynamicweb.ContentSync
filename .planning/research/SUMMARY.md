# Project Research Summary

**Project:** DynamicWeb.Serializer v2.0
**Domain:** CMS database serialization — pluggable provider architecture for DynamicWeb 10
**Researched:** 2026-03-23
**Confidence:** HIGH

## Executive Summary

DynamicWeb.Serializer v2.0 is a database serialization tool that extends the proven v1.3 content sync engine to cover the full DW10 database: ~74 SQL configuration tables, ~20 settings entries, and ~5 schema groups — in addition to the existing content tree. The approach is well-understood: build a pluggable `ISerializationProvider` interface (proven by Sitecore Unicorn and Metabase), drive all SQL table operations from DataGroup XML metadata already shipped with DW10, and wrap the existing ContentSerializer/ContentDeserializer as an adapter rather than rewriting it. No new NuGet packages are required — the existing Dynamicweb 10.23.9 meta-package ships all required APIs (`Dynamicweb.Data.Database`, `Dynamicweb.Configuration.SystemConfiguration`, CoreUI screens).

The recommended build order follows dependency chains: establish the provider interface and prove SqlTableProvider on one ecommerce table first (Wave 1), then migrate ContentProvider as an adapter to validate the architecture (Wave 2), then scale SqlTableProvider to all ecommerce settings tables while solving FK ordering and cache invalidation (Wave 3), then add SettingsProvider and SchemaProvider (Wave 4), then remaining SQL tables (Wave 5), and finally Admin UI changes including the log viewer, tree node relocation, and asset management action (Wave 6). This order ensures every subsequent wave builds on a working foundation and regressions are isolated to the layer being changed.

The primary risks are all in the Foundation wave: FK constraint violations from wrong table insertion order, identity column ID mismatches breaking cross-environment FK references, silent data loss for tables with empty NameColumn, and NULL/empty-string confusion in YAML round-trips. All four must be designed and verified before any ecommerce table is attempted. A secondary risk is breaking the working ContentProvider during architecture extraction — mitigated completely by using the adapter pattern and never touching ContentSerializer/ContentDeserializer internals.

## Key Findings

### Recommended Stack

The existing stack handles everything v2.0 needs. `Dynamicweb.Data.Database` + `CommandBuilder` is the canonical DW10 SQL access pattern (confirmed in official training docs and the AppStore app guide) and must be used for all SqlTableProvider operations — no raw `SqlConnection`, no Dapper, no EF Core. `SystemConfiguration.Instance` handles settings read/write. Existing CoreUI `ListScreenBase`/`EditScreenBase` patterns handle all Admin UI additions. YamlDotNet must stay on 13.7.1 — v14 introduced breaking API changes with no benefit.

**Core technologies:**
- `Dynamicweb.Data.Database` + `CommandBuilder`: all SQL reads/writes — DW10 canonical pattern, handles connection pooling internally
- `Dynamicweb.Configuration.SystemConfiguration`: settings key read/write — API confirmed, bulk prefix enumeration needs runtime validation
- `CoreUI ListScreenBase` / `EditScreenBase` / `NavigationNodeProvider`: Admin UI screens, already proven in v1.x
- `YamlDotNet 13.7.1`: YAML serialization — keep frozen, v14 is a breaking upgrade with no benefit
- `Microsoft.Extensions.Configuration.Json 8.0.1`: config file loading — already in project, no change needed
- `INFORMATION_SCHEMA.COLUMNS` + `sys.foreign_keys`: SQL Server metadata for schema export and FK dependency ordering

One gap: `SystemConfiguration.Instance.GetValue()` is confirmed for exact-path reads, but enumerating all child keys under a prefix path (needed for SettingsProvider) requires runtime validation. Direct XML parsing of `GlobalSettings.config` may be needed as fallback.

### Expected Features

**Must have (table stakes) — v2.0 core:**
- `ISerializationProvider` interface with Serialize/Deserialize/DryRun contract — foundation all else depends on
- Provider registry mapping DataType strings to provider instances — routing layer
- ContentProvider adapter wrapping existing ContentSerializer/ContentDeserializer unchanged — validates design
- SqlTableProvider — generic, metadata-driven, covers all 74 SQL-based data groups
- Identity resolution via NameColumn with CompareColumns/PK fallback — required for cross-environment upsert
- Predicate extension with `providerType` + `dataGroupId` fields — users choose what to serialize
- Ecommerce settings tables (~15 tables: OrderFlows, OrderStates, Payment, Shipping, Countries, Currencies, VAT) — highest-value proof of SqlTableProvider
- Structured result objects from all providers — foundation for log viewer and error reporting
- Source-wins conflict strategy, consistent across all providers

**Should have — v2.x:**
- Dependency-aware deserialization ordering (FK topological sort) — needed for reliable multi-table deserialize
- SettingsProvider (~20 settings items, KeyPatterns-based)
- SchemaProvider (~5 schema items, additive-only ALTER TABLE)
- Remaining SQL tables: Users, Marketing, PIM, Apps (~30 tables)
- Log viewer with guided advice (parse structured log entries, show actionable summaries)
- Asset management deserialize action (contextual zip-based workflow)
- Scheduled task removal (deprecate with warning in v2.0, remove in v3.0)

**Defer to v3+:**
- DataGroup auto-discovery from DW XML metadata at runtime
- Provider-specific row filtering predicates (e.g., "only EcomOrderFlows where Active=true")
- Custom provider SDK for third-party extensions
- Cross-environment rename detection via CompareColumns fuzzy matching

**Anti-features to reject explicitly:**
- Bidirectional merge / conflict resolution — source-wins is the correct answer, same as Unicorn and Metabase
- Incremental/delta sync — DW config tables lack uniform modification timestamps; full serialize is fast enough
- Real-time notification-based auto-serialize — slow in request context, race conditions
- Provider-level parallelism during deserialize — FK ordering requires sequential execution
- Schema migration (ALTER TABLE) as part of data serialization — use DW's own migration system

### Architecture Approach

The architecture adds a thin provider layer over the existing implementation without changing internal serializer logic. `SerializerOrchestrator` replaces direct `ContentSerializer` instantiation in API commands and iterates configured providers. `ContentProvider` is a pure adapter — it constructs a `SyncConfiguration` from the provider predicate and delegates to the unchanged `ContentSerializer`/`ContentDeserializer`. `SqlTableProvider` is metadata-driven: it reads DataGroup XML files via `DataGroupReader` to get table name, NameColumn, and CompareColumns, then runs generic SELECT/INSERT/UPDATE via the DW Database API. Provider output is separated into prefixed subdirectories (`_content/`, `_sql/`, `_settings/`, `_schema/`) to prevent collision. Backward compatibility is maintained: predicates without `providerType` default to `"Content"`, so existing v1.x configs load unchanged.

**Major components:**
1. `ISerializationProvider` + `SerializationProviderBase` — contract and shared YAML/logging helpers
2. `ProviderRegistry` — maps providerType strings to provider instances at runtime
3. `SerializerOrchestrator` — iterates predicates, dispatches to providers, aggregates results
4. `ContentProvider` — adapter wrapping ContentSerializer/ContentDeserializer (zero internal changes)
5. `SqlTableProvider` — generic SQL table provider driven by DataGroup XML metadata
6. `DataGroupReader` — parses DataGroup XMLs to extract table/column/cache metadata
7. `FlatFileStore` — per-row YAML I/O for SQL tables (distinct from FileSystemStore's mirror-tree layout)
8. `SerializerConfiguration` — extends SyncConfiguration with providerType per predicate
9. `LogViewerScreen` — reads structured log entries, surfaces actionable guidance
10. `AssetManagementDeserializeInjector` — deserialize action on .zip files in DW asset management

### Critical Pitfalls

1. **FK constraint violations from wrong table insertion order** — Query `sys.foreign_keys` at deserialization startup, topologically sort all scheduled tables before processing any. Wrap each DataGroup in a SQL transaction for clean rollback. Must be solved in the Foundation wave before any ecommerce table is touched.

2. **Identity column ID mismatches breaking cross-environment FK references** — Never use numeric PKs as canonical identifiers. Use NameColumn as the match key (same as DW's own Deployment tool). Maintain a `Dictionary<tableName, Dictionary<sourceId, targetId>>` runtime map during deserialization; translate FK column values through this map when processing child tables. Never use `SET IDENTITY_INSERT ON`.

3. **Silent data loss for tables with empty NameColumn** — At DataGroup parse time, classify each table into a match strategy: Named (NameColumn populated), Composite (CompareColumns populated), PK-based (query `sys.key_constraints`), or Full-row hash. Log the strategy for every table. Fail loudly if no viable strategy exists. Affects ~10+ tables including critical junction tables (`EcomCountries`, `EcomOrderStateRules`, `EcomVatCountryRelations`).

4. **Breaking ContentProvider during architecture extraction** — Do NOT touch ContentSerializer/ContentDeserializer internals. The ContentProvider adapter wraps them from outside. Run all existing unit tests and verify full round-trip matches v1.3 output byte-for-byte before proceeding to Wave 3.

5. **Serializing environment-specific settings values** — Maintain a blocklist for credentials, SMTP, payment gateway params, domain URLs, page IDs. Serialize all settings to YAML but tag environment-specific keys with `_envSpecific: true`. Skip them on deserialization by default. No recovery if payment gateway credentials are overwritten.

6. **NULL vs empty string corruption in SQL serialization** — Always emit explicit `null` (`~`) for SQL NULL; explicit `""` for empty strings; absent key means "preserve existing." Establish this format in the Foundation wave — retroactively fixing serialized files across 74 tables is prohibitively expensive.

7. **Duplicate DataItemType across multiple DataGroups** — `EcomMethodCountryRelation` appears in both Payment and Shipping DataGroup XMLs. Track processed tables in a global session registry; merge rather than replace on subsequent encounters.

## Implications for Roadmap

Based on combined research, the six-wave plan from ARCHITECTURE.md is strongly recommended. The dependency chain is clear and the risk ordering is well-justified.

### Phase 1: Provider Architecture Foundation

**Rationale:** `ISerializationProvider` is the foundation every subsequent feature depends on. SqlTableProvider covers 74/124 data groups (highest single-provider leverage). Proving it on EcomOrderFlow de-risks the entire architecture before scale work begins. All critical pitfalls (FK ordering, identity handling, NameColumn matching, NULL handling, duplicate DataItemType) must be solved here — they cannot be retrofitted later.

**Delivers:** `ISerializationProvider`, `SerializationProviderBase`, `ProviderRegistry`, `DataGroupReader`, `SqlColumnMapper`, `FlatFileStore`, `SqlTableProvider.Serialize()` + `Deserialize()`, proven round-trip on EcomOrderFlow.

**Addresses (FEATURES.md P1):** Provider interface, provider registry, SqlTableProvider, identity resolution, structured result objects.

**Avoids (PITFALLS.md):** FK constraint violations (P1), identity ID mismatches (P2), empty NameColumn data loss (P4), NULL/empty string corruption (P7), duplicate DataItemType (P8).

**Research flag:** NEEDS PHASE RESEARCH — FK topological sort implementation, `sys.foreign_keys` query, ID mapping dictionary design, and whether DW FK constraints are enforced at the SQL Server level or application level.

### Phase 2: Content Migration (Adapter + Orchestrator)

**Rationale:** Wrapping existing content code in the provider interface validates the architecture with zero new functionality risk. If the interface can't cleanly accommodate ContentSerializer, the design is wrong — better to discover this on a low-risk adapter than after three waves of SqlTableProvider work. Orchestrator must work before adding more providers.

**Delivers:** `ContentProvider` adapter, `SerializerOrchestrator`, updated `ConfigLoader` (providerType defaulting to "Content"), updated API commands using orchestrator. All existing tests pass unchanged.

**Addresses (FEATURES.md P1):** ContentProvider migration, predicate extension (providerType field), config backward compatibility.

**Avoids (PITFALLS.md):** ContentProvider regression (P3), config backward compatibility breakage (P6).

**Research flag:** STANDARD PATTERNS — adapter pattern is well-documented. Existing codebase is the primary reference.

### Phase 3: Ecommerce Settings Tables (SqlTableProvider at Scale)

**Rationale:** Ecommerce tables have the highest FK complexity (OrderFlow -> OrderStates -> OrderStateRules; Countries referenced by Payment/Shipping/VAT). Solving ordering and cross-DataGroup dependency here informs simpler remaining tables. These are also the highest-value tables for users (deployment workflows invariably involve ecommerce config).

**Delivers:** ~15 ecommerce settings tables serialized. FK ordering proven at scale. ServiceCache invalidation working. Tables without NameColumn handled via fallback match strategies.

**Addresses (FEATURES.md P1):** Ecommerce tables — proves "full database serialization" claim for core audience.

**Avoids (PITFALLS.md):** FK violations at scale (P1), cross-DataGroup duplicate tables (P8), ServiceCache staleness.

**Research flag:** STANDARD PATTERNS — FK metadata from `sys.foreign_keys` is well-documented. DataGroup XMLs already inspected. ServiceCache API (`Dynamicweb.Caching.CacheManager`) needs verification at implementation time.

### Phase 4: SettingsProvider + SchemaProvider

**Rationale:** Can proceed in parallel with Phase 3 (depends only on Phase 1 provider interface). SettingsProvider is simpler than SqlTableProvider (no FK concerns, no identity mapping). SchemaProvider is read-only export. Both complete coverage of DW10's 4 data group types.

**Delivers:** `SettingsProvider` serializing ~20 settings items by KeyPatterns. `SchemaProvider` exporting ~5 table schemas. Environment-specific settings blocklist protecting credentials.

**Addresses (FEATURES.md P2):** Settings serialization, schema export.

**Avoids (PITFALLS.md):** Environment-specific settings overwrite (P5) — blocklist must ship WITH SettingsProvider, not after.

**Research flag:** NEEDS PHASE RESEARCH — `SystemConfiguration.Instance` bulk key enumeration under path prefix may require direct `GlobalSettings.config` XML parsing as fallback. Needs runtime validation before implementation begins.

### Phase 5: Remaining SQL Tables

**Rationale:** Same SqlTableProvider pattern, more coverage. Users, Marketing, PIM, Apps tables (~30 tables). SqlTableProvider is proven at scale on ecommerce tables; remaining tables are incremental.

**Delivers:** Full coverage of all ~74 SqlDataItemProvider data groups.

**Addresses (FEATURES.md P2):** Remaining SQL tables (Users, Marketing, PIM, Apps).

**Avoids:** No new pitfall categories — same patterns as Phase 3. Table-specific quirks addressed case-by-case.

**Research flag:** STANDARD PATTERNS — same SqlTableProvider mechanics as Phase 3.

### Phase 6: Admin UI + Log Viewer + Asset Management

**Rationale:** UI changes are cosmetic and don't affect the serialization pipeline. They depend on config format being finalized (Phases 1-2) and structured log output being in place (Phases 1-3). Doing UI last means rename and tree node relocation are a single coordinated change with no intermediate breakage.

**Delivers:** Tree node relocated from Content > Sync to Database > Serialize. `LogViewerScreen` with structured log parsing and guided advice. `AssetManagementDeserializeInjector` for zip-based workflow. ScheduledTasks deprecated with warning (not removed — removal in v3.0).

**Addresses (FEATURES.md P2):** Log viewer, asset management deserialize action, scheduled task deprecation.

**Avoids (PITFALLS.md):** Tree node relocation without redirect (keep old node as deprecation redirect). Scheduled task removal without migration path (deprecate only in v2.0).

**Research flag:** NEEDS PHASE RESEARCH — DW asset management extension point for file detail screen injector needs verification. Tree node parent section "Database > Serialize" needs confirmation that DW10 exposes this section or how to register a new one.

### Phase Ordering Rationale

- Provider interface (Phase 1) must come first because 5 of 8 critical pitfalls must be solved at the foundation level and cannot be retrofitted.
- Content migration (Phase 2) before ecommerce tables (Phase 3) because the orchestrator must be working and tested before adding more load, and ContentProvider is the lowest-risk validation case.
- Ecommerce tables (Phase 3) before remaining SQL tables (Phase 5) because FK ordering complexity is highest in ecommerce — solving it here makes Phase 5 incremental.
- SettingsProvider (Phase 4) can run in parallel with Phase 3 if team capacity allows, since it depends only on Phase 1.
- Admin UI (Phase 6) last because: (a) UI depends on finalized config format; (b) log viewer depends on structured log output from providers; (c) rename/relocation is safest as a single final coordinated change.

### Research Flags

Phases needing deeper research during planning:
- **Phase 1 (Foundation):** Whether DW ecommerce table FK constraints are enforced at the SQL Server level or application level. FK topological sort implementation. ID mapping dictionary design across providers.
- **Phase 4 (Settings/Schema):** `SystemConfiguration.Instance` bulk key enumeration under a path prefix. Must be validated at runtime before designing SettingsProvider.
- **Phase 6 (Admin UI):** DW asset management extension point for injecting a "Deserialize" action on zip files. Tree node parent section for "Database > Serialize."

Phases with standard patterns (skip research-phase):
- **Phase 2 (Content Migration):** Pure adapter pattern. Existing codebase is the authoritative reference.
- **Phase 3 (Ecommerce Tables):** SqlTableProvider mechanics proven in Phase 1. FK metadata from `sys.foreign_keys` is standard SQL Server.
- **Phase 5 (Remaining Tables):** Same mechanics as Phase 3.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All APIs confirmed in official DW10 training docs, AppStore guide, and existing v1.x usage. No new packages needed. Only gap is SystemConfiguration bulk enumeration (MEDIUM for that specific API). |
| Features | HIGH | Strong patterns from Unicorn, Metabase, and v1.x codebase. DataGroup XML analysis confirms the 74/20/5 breakdown. Anti-features are clearly defined with rationale. |
| Architecture | HIGH | Based on direct codebase analysis and ~100 DataGroup XML inspections. Component boundaries are clean. Wave ordering is well-justified by dependency chains. |
| Pitfalls | HIGH | Based on direct codebase analysis (937 LOC ContentDeserializer), ~100 DataGroup XML files inspected, and v1.0-v1.3 DW API development experience. All 8 critical pitfalls have concrete prevention strategies. |

**Overall confidence:** HIGH

### Gaps to Address

- **SystemConfiguration bulk key enumeration:** Does `SystemConfiguration.Instance` support enumerating all child keys under a path prefix? If not, need direct `GlobalSettings.config` XML parsing. Validate at runtime before designing SettingsProvider (Phase 4 planning).

- **DW FK constraint enforcement level:** Are DW ecommerce table FKs enforced at the SQL Server level or only application-layer? If application-layer only, topological sort is still recommended for data integrity but violations won't manifest as `SqlException`. Validate by querying `sys.foreign_keys` before Phase 1 implementation.

- **ServiceCache invalidation API:** DataGroup XMLs list ServiceCache class names (e.g., `Dynamicweb.Ecommerce.Orders.Discounts.DiscountService`). Presumed mechanism is `Dynamicweb.Caching.CacheManager`. Needs verification before Phase 3 implementation.

- **DW asset management injector extension point:** The CoreUI extension point for adding actions to the file detail screen in asset management needs runtime verification. Handle in Phase 6 planning.

- **Project rename scope:** PITFALLS.md strongly recommends the rename (Dynamicweb.ContentSync -> DynamicWeb.Serializer) happens either first or last, never in the middle. If done last (Phase 6), no special action needed now. If done first, a migration helper for config path, log path, and scheduled task type names stored in the DW database must be built in Phase 1.

## Sources

### Primary (HIGH confidence)
- DW10 Core Concepts Training — CommandBuilder, Database class, parameterized SQL patterns
- DW10 AppStore App Guide — full CRUD pattern with CommandBuilder confirmed
- DW10 Screen Types documentation — ListScreenBase, EditScreenBase, NavigationNodeProvider
- `Database.CreateDataReader` / `ExecuteNonQuery` / `ExecuteScalar` API reference
- Sitecore Unicorn GitHub — provider architecture, predicate system, source-wins strategy
- Metabase Serialization docs — YAML format, entity name-based identity resolution
- SQL Server `sys.foreign_keys` / `INFORMATION_SCHEMA` — FK dependency ordering pattern
- Direct codebase analysis — `ContentSerializer.cs` (158 LOC), `ContentDeserializer.cs` (937 LOC), v1.3 full source
- DataGroup XML files (~100 files at `C:\temp\DataGroups\`) — table metadata, NameColumn patterns, ServiceCaches

### Secondary (MEDIUM confidence)
- Dynamicweb.Configuration Namespace API docs — SystemConfiguration confirmed, bulk enumeration uncertain
- v2.0 pivot document (`project_v2_pivot.md`) — wave plan, data group breakdown
- DW DataPortability NuGet — current package version reference

### Tertiary (needs runtime validation)
- `SystemConfiguration.Instance` child key enumeration — underdocumented, needs runtime test
- DW asset management extension point for file detail screen injector — needs DW source inspection
- FK constraint enforcement level in DW10 ecommerce tables — needs `sys.foreign_keys` query on target DB

---
*Research completed: 2026-03-23*
*Ready for roadmap: yes*
