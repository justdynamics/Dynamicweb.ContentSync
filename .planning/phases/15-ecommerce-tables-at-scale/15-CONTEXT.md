# Phase 15: Ecommerce Tables at Scale - Context

**Gathered:** 2026-03-24
**Status:** Ready for planning

<domain>
## Phase Boundary

Scale SqlTableProvider from 1 proof table to all non-transactional ecommerce tables (~24 tables). Add FK dependency ordering via runtime topological sort, DW service cache invalidation after deserialization (for ALL providers, not just SQL), and config predicates for all ecommerce categories. Exclude EcomOrders (transactional data).

</domain>

<decisions>
## Implementation Decisions

### Table Scope
- **D-01:** All non-transactional ecommerce tables (~24 unique tables) from Orders, Internationalization, and related DataGroups
- **D-02:** Exclude EcomOrders — that's transactional data, not settings/config
- **D-03:** Specific categories: OrderFlows/States/StateRules, Payment, Shipping, Stock, OrderFields (excluding EcomOrders), OrderLineFields, OrderContexts, TrackAndTrace, AddressValidation, ValidationGroups, Countries, Languages, Currencies, VATGroups

### FK Dependency Ordering
- **D-04:** Runtime topological sort via `sys.foreign_keys` — query FK metadata from SQL Server, build dependency graph, sort tables so parents are deserialized before children
- **D-05:** Single pass deserialization (not DW's two-pass approach) — simpler, and ecommerce tables should form an acyclic dependency graph
- **D-06:** If circular FK dependencies detected, fail with clear error message listing the cycle — don't silently break

### Duplicate Table Handling
- **D-07:** Not a problem with our architecture — predicates reference tables directly by name, not DataGroup IDs. Each table configured once in predicates. If user accidentally configures the same table twice, second serialize overwrites same folder and second deserialize skips (checksums match). No dedup logic needed.

### Cache Invalidation (NEEDS RESEARCH)
- **D-08:** Cache invalidation is per-predicate/per-table, not a blanket clear — each provider knows which caches to invalidate based on what was deserialized
- **D-09:** Cache invalidation applies to ALL providers (Content AND SqlTable), not just SQL tables — content deserialization also leaves stale caches
- **D-10:** Research required: investigate DW10 source for cache types, clear APIs, and how the Deployment tool maps tables to caches. The DataGroup XMLs have `<ServiceCaches>` sections as a starting point.
- **D-11:** Cache invalidation should be configurable per predicate in config (which caches to clear after this predicate runs)

### Claude's Discretion
- Topological sort algorithm choice (Kahn's vs DFS)
- How to integrate FK ordering into the orchestrator (sort predicates before dispatch, or sort within SqlTableProvider)
- Config predicate structure for the ~24 ecommerce tables (one predicate per table vs grouped)
- Test strategy for FK ordering (mock FK metadata or use real DB schema)

</decisions>

<specifics>
## Specific Ideas

- The DW Deployment tool's DataGroup XMLs have `<ServiceCaches>` sections listing which caches to clear — use these as reference during research
- EcomMethodCountryRelation appears in both Payment and Shipping DataGroups in DW's XML, but we configure it once as a single predicate — no dedup needed
- EcomOrderFlow/OrderStates/OrderStateRules appear in OrderFlows, CartFlows, AND QuoteFlows DataGroups — same principle, configure once
- Cache invalidation page in DW docs: https://doc.dynamicweb.dev/manual/dynamicweb10/settings/system/developer/cache.html

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### DW10 Source (cache invalidation research)
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Deployment\LocalDeploymentProvider.cs` — How Deployment tool clears caches after import
- `C:\temp\DataGroups\Settings_Ecommerce_Orders_060_OrderFlows.xml` — Example DataGroup with `<ServiceCaches>` section
- `C:\Projects\temp\dw10source\` — Search for cache clearing APIs, ServiceCache types, CacheManager

### DW10 Source (FK metadata)
- SQL Server `sys.foreign_keys` + `sys.foreign_key_columns` — runtime FK dependency discovery
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Data\DataItemProviders\SqlDataItemWriter.cs` — How DW handles FK constraints during writes

### Existing Codebase (Phase 13 + 14)
- `src\Dynamicweb.ContentSync\Providers\SqlTable\SqlTableProvider.cs` — Working single-table provider to extend
- `src\Dynamicweb.ContentSync\Providers\SerializerOrchestrator.cs` — Dispatches predicates, needs FK ordering integration
- `src\Dynamicweb.ContentSync\Providers\SqlTable\DataGroupMetadataReader.cs` — Builds TableMetadata from predicate + DB schema
- `src\Dynamicweb.ContentSync\Providers\SqlTable\SqlTableWriter.cs` — MERGE upsert, may need cache invalidation hook
- `src\Dynamicweb.ContentSync\Providers\Content\ContentProvider.cs` — Content adapter, also needs cache invalidation

### DataGroup XML Files (table inventory)
- `C:\temp\DataGroups\Settings_Ecommerce_Orders_*.xml` — All order-related tables with NameColumn/CompareColumns
- `C:\temp\DataGroups\Settings_Ecommerce_Internationalization_*.xml` — Countries, Languages, Currencies, VAT tables

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **SqlTableProvider**: Already handles single-table serialize/deserialize — scales to multiple tables via config predicates
- **SerializerOrchestrator**: Dispatches predicates to providers — FK ordering can be integrated here (sort SqlTable predicates before dispatch)
- **DataGroupMetadataReader**: Queries PK, identity, all columns from DB — extend to also query FK relationships
- **ISqlExecutor**: Testable abstraction for all DB queries including sys.foreign_keys

### Established Patterns
- **Predicate-per-table config**: Each table is a separate predicate with table/nameColumn/compareColumns
- **MERGE upsert**: Working for single table, same pattern applies to all tables
- **Checksum skip detection**: Unchanged rows skipped automatically

### Integration Points
- **FK ordering hooks into orchestrator**: Before dispatching SqlTable predicates, sort them by FK dependency
- **Cache invalidation hooks into provider**: After Deserialize completes, provider calls cache clear for relevant caches
- **Config grows**: ~24 new predicates in config for ecommerce tables

</code_context>

<deferred>
## Deferred Ideas

- Settings & Schema providers — future milestone scope
- Users, Marketing, PIM tables — future milestone scope
- DataGroup auto-discovery — future, enumerate available tables from DW metadata
- Batch predicate config (one predicate = all tables in a DataGroup) — future UX improvement

</deferred>

---

*Phase: 15-ecommerce-tables-at-scale*
*Context gathered: 2026-03-24*
