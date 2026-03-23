# Pitfalls Research

**Domain:** Adding pluggable provider architecture, SQL table serialization, settings serialization, and schema serialization to an existing DynamicWeb content sync tool
**Researched:** 2026-03-23
**Confidence:** HIGH (based on direct codebase analysis of v1.3 implementation, inspection of ~100 DataGroup XML files, DW API experience from v1.0-v1.3 development, and SQL Server FK/identity behavior)

## Critical Pitfalls

### Pitfall 1: FK Constraint Violations During SQL Table Deserialization

**What goes wrong:**
SqlTableProvider deserializes tables in arbitrary order. Junction and relation tables (e.g. `EcomMethodCountryRelation`, `EcomVatCountryRelations`, `EcomOrderStateRules`) hold foreign keys referencing parent tables (`EcomPayments`, `EcomCountries`, `EcomOrderStates`). Inserting child rows before parent rows exist causes FK constraint violations and cascading failures across the entire DataGroup.

**Why it happens:**
DataGroup XMLs list multiple DataItemTypes in a single group. For example, `Settings_Ecommerce_Orders_060_OrderFlows.xml` lists three tables: `EcomOrderFlow`, `EcomOrderStates`, `EcomOrderStateRules`. The ordering within the XML may or may not reflect FK dependencies. Developers process tables in document order and assume the XML author encoded the dependency chain correctly. They also forget that dependencies can span across DataGroups -- `EcomMethodCountryRelation` depends on `EcomCountries` from a different DataGroup XML.

**How to avoid:**
1. Query `sys.foreign_keys` and `sys.foreign_key_columns` from SQL Server at deserialization startup to build a table dependency graph.
2. Topologically sort all tables scheduled for deserialization before processing any.
3. For cross-DataGroup dependencies (e.g. shipping/payment both depending on countries), ensure the countries DataGroup is deserialized first.
4. Within a DataGroup, use the XML document order as a hint but validate it against the FK graph.
5. Wrap each DataGroup in a transaction so partial failures roll back cleanly.

**Warning signs:**
- `SqlException` mentioning `FOREIGN KEY constraint` during deserialization
- Junction tables (anything with "Relation", "Rules", or "Link" in the table name) failing while parent tables succeed
- Deserialization succeeds on populated databases but fails on empty ones

**Phase to address:**
Foundation phase -- the table execution order logic must be baked into SqlTableProvider core before any ecommerce tables are attempted.

---

### Pitfall 2: Identity Column ID Collision and Mismatched FK References

**What goes wrong:**
Most ecommerce tables use auto-increment `int` identity columns as primary keys (unlike content pages which have GUIDs). Serializing numeric IDs from the source environment and trying to use them for matching in the target causes three failure modes: (a) `SET IDENTITY_INSERT ON` fails or is left ON after an error, corrupting future inserts; (b) source ID 5 matches target ID 5 which is a completely different row; (c) child table FK values point to source IDs that map to wrong target rows.

**Why it happens:**
The existing ContentProvider solved cross-environment identity with `PageUniqueId` (GUID). Developers assume they can port this pattern to SQL tables, but the vast majority of DW ecommerce tables have no GUID column. The temptation is to use the numeric PK as the match key, which silently works on same-environment round-trips but breaks on cross-environment syncs.

**How to avoid:**
1. Use `NameColumn` from the DataGroup XML as the primary match key. This is what DW's own Deployment tool uses.
2. Never serialize identity column values as canonical identifiers. Include them in YAML only as `_sourceId` metadata for debugging.
3. Maintain a runtime `Dictionary<string, Dictionary<int, int>>` mapping source IDs to target IDs, keyed by table name. When deserializing child tables, translate FK column values through this map.
4. For tables with empty `NameColumn` (see Pitfall 4), use composite keys from non-identity columns.
5. Let SQL Server assign new IDs naturally -- never use `SET IDENTITY_INSERT ON`.

**Warning signs:**
- Deserialized rows overwrite unrelated existing rows (ID collision)
- FK columns in child tables point to wrong parent rows after deserialization
- "Cannot insert explicit value for identity column" errors
- Data looks correct on source but relationships are scrambled on target

**Phase to address:**
Foundation phase. The ID mapping strategy is an architectural decision that cascades to every subsequent table. Must be decided and implemented before the first SqlTableProvider test case.

---

### Pitfall 3: Breaking Existing ContentProvider During Provider Architecture Extraction

**What goes wrong:**
Wrapping the working `ContentSerializer`/`ContentDeserializer` into a provider interface changes method signatures, constructor injection patterns, or configuration loading paths. Subtle regressions emerge: predicates stop filtering correctly, GUID matching breaks on edge cases, dry-run mode loses field-level diffs, permission mapping silently stops, or the cascade-skip error handling in `DeserializePageSafe` loses its `FailedParentGuids` tracking.

**Why it happens:**
The current `ContentSerializer` is 158 LOC with tightly coupled internal state: `WriteContext` (mutable state through recursive tree walk), `ReferenceResolver` (cross-reference cache), `ContentPredicateSet` (filtering), and `PermissionMapper`. The `ContentDeserializer` is even more complex at 937 LOC with nested error handling, dry-run branching, and template validation. Extracting an `ISerializationProvider` interface and threading new configuration through these classes touches every constructor and method signature.

**How to avoid:**
1. Do NOT refactor ContentSerializer/ContentDeserializer internals. Create a `ContentProvider` adapter class that implements the new `ISerializationProvider` interface and delegates to the existing classes unchanged.
2. The provider interface should be minimal: `void Serialize(ProviderConfig config)` and `ProviderResult Deserialize(ProviderConfig config)`. The ContentProvider adapter translates between `ProviderConfig` and `SyncConfiguration`.
3. Run the complete existing test suite after wrapping -- `PermissionSerializationTests`, `PermissionDeserializationTests`, `YamlRoundTripTests`, `ConfigLoaderTests`, `ContentPredicateTests` must all pass without modification.
4. Verify a full round-trip (serialize from Swift 2.2, deserialize into Swift 2.1) produces identical results to v1.3 before and after the wrapping.
5. Only extract shared logic (YAML writing, logging patterns) into the provider base class AFTER the adapter pattern is proven stable.

**Warning signs:**
- Any existing unit test fails after refactoring
- Content round-trip produces different YAML output or different DB state than v1.3
- New provider interface forces changes to `SyncConfiguration` that break config file backward compatibility
- `WriteContext`, `ReferenceResolver`, or `ContentPredicateSet` internals leak into the provider interface

**Phase to address:**
Content migration phase (Wave 2). This should be a standalone phase with zero feature additions -- purely the wrapping/adapter work with comprehensive regression testing.

---

### Pitfall 4: Empty NameColumn in DataGroup XMLs Causes Silent Data Loss

**What goes wrong:**
Approximately 10+ DataGroup XMLs have `NameColumn=""` (empty string). Confirmed examples: `EcomCountries`, `EcomOrderStateRules`, `EcomVatCountryRelations`, `EcomAssortmentPermissions`, `EcomOrderFlowStepRelation`. If SqlTableProvider uses NameColumn as the match key and it is empty, the matching logic either (a) treats every row as "matching" the first row (all rows collapse to one), (b) treats every row as "no match" (all rows inserted as duplicates), or (c) throws a null reference exception.

**Why it happens:**
Developers build the happy path where NameColumn is populated (e.g. `OrderFlowName`, `PaymentName`, `CurrencyName`) and treat empty NameColumn as a rare edge case. But it affects at least 10 tables across ecommerce, marketing, PIM, and apps domains -- including critical junction tables that hold relationship data.

**How to avoid:**
1. At DataGroup XML parse time, classify each DataItemType into a match strategy:
   - **Named**: `NameColumn` is non-empty. Match rows by this column value.
   - **Composite**: `NameColumn` is empty but `CompareColumns` is populated. Use CompareColumns as composite match key.
   - **PK-based**: Both empty. Query `sys.key_constraints` and `sys.index_columns` to find the primary key columns. Use those as match key.
   - **Full-row**: No PK discoverable (pure heap table, extremely rare). Hash all column values for matching.
2. Log the chosen match strategy for every table at serialization time so the decision is auditable.
3. Add explicit validation: if a table has no viable match strategy, fail loudly rather than silently losing data.

**Warning signs:**
- A table with 50 rows serializes correctly but deserializes to 1 row or 100 rows
- Junction table data (country-method relations, VAT-country relations) disappears after round-trip
- "Duplicate key" SQL errors on tables that should have been matched/updated

**Phase to address:**
Foundation phase, specifically the DataGroup XML parser and SqlTableProvider match strategy logic. This must be resolved before any table serialization is attempted.

---

### Pitfall 5: Serializing Environment-Specific Settings Values

**What goes wrong:**
`SettingsDataItemProvider` serializes GlobalSettings key-value pairs based on `KeyPatterns`. Many of these contain values that are correct only for the source environment: SMTP server addresses and credentials, API keys and secrets, payment gateway merchant numbers (`PaymentMerchantNum`, `PaymentGatewayMD5Key`), connection strings, license keys, absolute file paths, domain URLs, scheduled task configurations, and numeric page IDs that differ between environments (like `PageNotFound` page ID).

Deserializing these into a different environment silently overwrites working configuration. The target environment's SMTP stops sending, payment processing breaks, and links to system pages 404.

**Why it happens:**
Settings look like harmless key-value pairs. The DataGroup XMLs specify broad patterns like `/Globalsettings/Settings/CommonInformation` which captures everything under that path -- including environment-specific values mixed in with genuinely portable settings. There is no metadata in the DataGroup XML distinguishing portable from environment-specific settings.

**How to avoid:**
1. Maintain a blocklist of key patterns known to be environment-specific:
   - `*Smtp*`, `*ConnectionString*`, `*Password*`, `*Secret*`, `*Key*` (API keys, not label keys)
   - `*MerchantNum*`, `*GatewayMD5*`, `*GatewayParameters*` (payment credentials)
   - `*PageNotFound*`, `*ForceSSL*`, `*Cdn*` (domain/infrastructure)
   - `/Globalsettings/System/ScheduledTasks` (scheduled task configs)
2. Serialize ALL settings to YAML for completeness, but tag environment-specific keys with `_envSpecific: true` in the YAML output.
3. On deserialization, skip keys tagged as environment-specific by default. Provide a `--include-env-specific` flag for same-environment restores.
4. Show a clear summary in the deserialization log: "Skipped 12 environment-specific settings. Use --include-env-specific to override."

**Warning signs:**
- After deserialization, outbound emails stop working
- Payment processing returns gateway errors
- Scheduled tasks run with wrong parameters or target wrong pages
- SSL/domain configuration breaks the target site
- Page-not-found settings point to non-existent page IDs

**Phase to address:**
Settings serialization phase (Wave 4). The blocklist and environment-specific tagging must be designed before the first settings provider ships.

---

### Pitfall 6: Project Rename Breaking Existing DW Installations

**What goes wrong:**
Renaming from `Dynamicweb.ContentSync` to `DynamicWeb.Serializer` changes:
- Assembly name (DW discovers UI providers, commands, screens by assembly scanning)
- Root namespace (all type references change)
- NuGet package ID
- Config file search path (`Files/System/ContentSync/` -> `Files/System/Serializer/`)
- Log file path (`Files/System/ContentSync/Log/` -> new path)
- Scheduled task type names stored in the DW database (`Dynamicweb.ContentSync.ScheduledTasks.SerializeScheduledTask`)
- Admin UI tree node IDs (`ContentSync_Settings` -> new ID, breaking bookmarks)

Users who upgrade from v1.x find: admin UI nodes disappear, config file not found (no predicates loaded), scheduled tasks throw "type not found" at next execution, log viewer shows empty because it looks at new path while logs are at old path.

**Why it happens:**
Developers rename everything in one commit, test on a fresh install, and declare success. They forget that existing installations have database records referencing old fully-qualified type names and files at old paths.

**How to avoid:**
1. Ship a migration helper that runs on first load after upgrade:
   - Check for config at old path (`ContentSync/`), copy to new path if new path has no config
   - Check for log files at old path, create symlink or copy
   - Scan DW scheduled task table for old type names, update to new type names
2. Register `[Obsolete]` type aliases in the old namespace that forward to new implementations (DW's add-in system may support `TypeForwardedTo` or similar)
3. Keep the config file path unchanged (`ContentSync/`) for backward compatibility -- only change UI labels. Alternatively, support both paths with old-path-as-fallback.
4. Do NOT rename in the same phase as adding provider architecture. Two breaking changes in one phase makes debugging regression sources impossible.

**Warning signs:**
- Admin UI shows blank/missing node under Settings after upgrade
- Log: "Type 'Dynamicweb.ContentSync.ScheduledTasks.SerializeScheduledTask' not found"
- Config loads with zero predicates on upgraded installation
- Users report "my settings are gone" after upgrade

**Phase to address:**
Rename should be either the very first phase (rename then build features) or the very last phase (features stable, then rename). Never in the middle. If done first, implement backward-compat migration upfront. If done last, all features are tested under old names, rename is a single coordinated change.

---

### Pitfall 7: NULL vs Empty String vs Default Value Confusion in SQL Serialization

**What goes wrong:**
SQL Server distinguishes `NULL`, empty string (`''`), and column defaults. YAML has `null` (`~`), empty string (`""`), and omitted keys. When SqlTableProvider serializes a NULL varchar column, it may emit `""` or omit the key entirely. On deserialization, `NULL` becomes `''` or vice versa. This breaks application logic that uses `IS NULL` checks -- particularly in DW ecommerce where `NULL` often means "inherit from parent/default" while `''` means "explicitly empty/overridden."

**Why it happens:**
YamlDotNet by default omits null values or serializes them as `~`. Developers round-trip test with non-null data and miss NULL edge cases. The existing ContentProvider handles NULL correctly for Item fields (explicit source-wins null-out) but SQL table columns have different semantics -- NULL has meaning distinct from empty.

**How to avoid:**
1. Always emit explicit `null` in YAML for SQL NULL values. Configure YamlDotNet to never omit null keys. Use `~` YAML syntax.
2. Always emit explicit `""` for empty strings. Never let empty strings become nulls.
3. Define clear deserialization semantics:
   - Key present with `null` value -> SET column = NULL
   - Key present with `""` value -> SET column = ''
   - Key absent from YAML -> Do not touch column (preserve existing value). This enables partial/selective serialization in the future.
4. For each column, check `IS_NULLABLE` metadata. If a column does not allow NULL, convert null to the column's declared default value or empty string.
5. Add round-trip tests specifically for NULL/empty/default columns on representative tables.

**Warning signs:**
- SQL columns that were NULL become empty strings after round-trip
- Application behavior changes subtly (e.g. "inherit from parent" settings become "explicitly empty")
- `SqlException: Cannot insert NULL into column 'X'` on non-nullable columns
- Decimal columns losing precision (e.g. `19.99` becoming `19.9900000000`)

**Phase to address:**
Foundation phase, core SqlTableProvider YAML serialization format. NULL handling must be established in the serialization format from the very first table.

---

### Pitfall 8: Duplicate DataItemType Across Multiple DataGroups

**What goes wrong:**
The table `EcomMethodCountryRelation` appears in BOTH `Settings_Ecommerce_Orders_010_Payment.xml` AND `Settings_Ecommerce_Orders_020_Shipping.xml`. If both DataGroups are deserialized independently, the relation table gets processed twice. Depending on implementation: (a) duplicate rows are inserted, (b) the second pass deletes rows from the first pass (if using source-wins with cleanup), or (c) FK references from the second pass overwrite correct mappings from the first.

**Why it happens:**
DW's Deployment tool processes DataGroups in a specific order and likely handles this internally. Our SqlTableProvider processes DataGroups independently without awareness of what other providers have already touched. The same table appearing in multiple XMLs is a DataGroup design pattern (shared junction table), not a bug.

**How to avoid:**
1. Build a global table registry that tracks which tables have been processed in the current serialization/deserialization session.
2. When SqlTableProvider encounters a table already in the registry, merge rather than replace -- add new rows, update existing rows by match key, but do not delete rows that were inserted by a previous DataGroup.
3. During serialization, deduplicate: only serialize a table once, even if it appears in multiple DataGroup XMLs.
4. Log when a duplicate DataItemType is detected: "Table EcomMethodCountryRelation already processed by Payment DataGroup, merging with Shipping DataGroup."

**Warning signs:**
- Junction tables have duplicate rows after full deserialization
- Relation data from one domain (payment) disappears after deserializing another domain (shipping)
- Row counts in junction tables increase with each deserialization run

**Phase to address:**
Foundation phase, during the DataGroup XML discovery and registration step. The deduplication registry must exist before any multi-DataGroup deserialization is attempted.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Hardcoding table insert order instead of querying FK metadata | Faster initial implementation (~1 hour saved) | Every new table requires manual ordering review. One missed dependency causes silent FK failures. Does not scale to 74 tables. | Never -- FK metadata query costs 2 extra hours and scales automatically |
| Using raw ADO.NET `SqlCommand` for all table reads/writes | Bypasses DW service layer, guaranteed SQL access | Misses DW caching hooks, ignores DW's column aliasing, breaks if DW renames columns in updates | Only for tables confirmed to have no DW Service API equivalent |
| Single monolithic SqlTableProvider for all 74 tables | Less code, one class to maintain | Cannot customize per-table behavior (e.g. PaymentGatewayParameters needs special handling, Products need variant awareness) | MVP/Foundation only -- plan for per-domain subclasses (EcommerceTableProvider, UserTableProvider) |
| Skipping `ServiceCaches` invalidation after deserialization | Faster deserialization, simpler code | DW admin UI shows stale data until app restart. Users think deserialization failed. | Never -- DataGroup XMLs list required ServiceCaches for exactly this reason |
| Storing source numeric IDs in YAML as primary identifiers | Simpler matching logic, works for same-environment round-trips | Cross-environment sync silently corrupts data by matching wrong rows. Impossible to fix retroactively without re-serializing everything. | Never |
| Processing settings key patterns as exact matches instead of path prefixes | Simpler implementation | Misses sub-keys. `/Globalsettings/Ecom/Unit` as exact match misses `/Globalsettings/Ecom/Unit/Weight`, `/Globalsettings/Ecom/Unit/Volume`, etc. | Never -- KeyPatterns are documented as path prefixes in DW's SettingsDataItemProvider |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| DataGroup XML: empty NameColumn | Treating empty NameColumn as "no matching needed" -- results in data loss or duplication | Empty NameColumn means "use alternative matching." Check CompareColumns first, fall back to SQL PK metadata. |
| DataGroup XML: Parameter elements | Assuming all three parameters (Table, NameColumn, CompareColumns) are always present | XSD allows any parameters. Some DataItemTypes have only `Table` (SchemaProvider), others have `Table` + `NameColumn` + `CompareColumns` (SqlProvider), SettingsProvider has `KeyPatterns`. Parse defensively. |
| DataGroup XML: ParentId hierarchy | Parsing XMLs as independent flat files | ParentId attributes build a tree: `040_Ecommerce` -> `Settings_Ecommerce_020_Orders` -> `Settings_Ecommerce_Orders_060_OrderFlows`. Build full tree for UI navigation and dependency ordering. |
| DW ServiceCaches | Ignoring `<ServiceCaches>` element entirely | After deserialization, iterate ServiceCaches and invalidate each one via `Dynamicweb.Caching.CacheManager`. Without this, DW UI shows pre-deserialization data until app restart. |
| SettingsDataItemProvider: KeyPatterns | Treating KeyPatterns as exact key names | KeyPatterns are comma-separated path prefixes. `/Globalsettings/Settings/CommonInformation` matches ALL keys under that subtree. Must enumerate all matching keys, not just the prefix itself. |
| EcomMethodCountryRelation duplication | Processing it independently in both Payment and Shipping DataGroups | Same table appears in multiple DataGroup XMLs. Track processed tables globally and merge on subsequent encounters. |
| SQL column type fidelity | Serializing all values as untyped YAML strings | `bit` columns need YAML `true`/`false`. `money`/`decimal` need exact precision strings. `datetime` needs ISO 8601. `uniqueidentifier` needs lowercase GUID format. Type-lossy serialization causes silent data corruption. |
| SchemaDataItemProvider | Trying to CREATE TABLE on target that already has the table | Schema provider must use `IF NOT EXISTS` guards for tables and `IF NOT EXISTS` for columns. Also handle column type changes (ALTER vs ADD). |
| CompareColumns parameter | Treating CompareColumns as "columns to compare for changes" | CompareColumns defines which columns to include in the serialized output. If empty, serialize ALL columns. If populated, serialize only listed columns (plus PK). This affects both serialization and deserialization scope. |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| `SELECT *` loading all rows into memory per table | High memory on large tables. `EcomProducts` can have 100K+ rows, `EcomOrderLines` even more. | Use streaming `SqlDataReader`, serialize row-by-row to YAML files. Never load entire table into `List<Dictionary>`. | Tables with >10K rows |
| Individual `INSERT`/`UPDATE` per row during deserialization | 74 tables x average 100 rows = 7,400+ DB round-trips. Large tables take minutes. | Use `SqlBulkCopy` for tables with >100 rows during initial insert. Use batched `UPDATE` statements. | Tables with >500 rows |
| Parsing all ~100 DataGroup XMLs at app startup | Adds 200-500ms to DW startup for XML I/O | Parse lazily on first access per DataGroup. Cache parsed models in memory. | Always noticeable during development; production impact depends on disk speed |
| Re-querying FK metadata on every deserialization run | Unnecessary SQL Server metadata queries each run | Cache FK dependency graph per database. Invalidate only when schema changes are detected. | Adds ~2s overhead per run for 74 tables |
| No transactions -- partial failures leave inconsistent state | Half of a DataGroup's tables deserialized, half failed. Junction tables reference rows that were rolled back. | Wrap each DataGroup's deserialization in a SQL transaction. Commit on full success, rollback on any failure. | Any failure scenario with multi-table DataGroups |
| Serializing all 74 SQL tables when user only changed ecommerce settings | Full serialization takes minutes, most output unchanged | Track which DataGroups the user selected for serialization. Only process selected groups. | Always -- no user wants to wait for full DB dump |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Serializing payment gateway credentials (`PaymentMerchantNum`, `PaymentGatewayMD5Key`, `PaymentGatewayParameters`) | Payment credentials exposed in YAML files, committed to git, accessible to anyone with repo access | Blocklist columns matching `*MerchantNum*`, `*MD5Key*`, `*GatewayParameters*`, `*Secret*`, `*Password*`. Redact with `[REDACTED]` placeholder. |
| Serializing settings with SMTP credentials, API keys | Credentials in plain text YAML in git | Blocklist known credential key patterns in SettingsProvider. Never serialize `/Globalsettings/System/Smtp/Password` or similar. |
| No authorization check on deserialization API command | Any authenticated DW admin can overwrite entire database configuration | Require explicit "Serializer Admin" permission, separate from general admin access. Log who triggered deserialization and when. |
| Serializing `AccessUser` table data (if added later) | User passwords, tokens, personal data in YAML files -- GDPR violation | User tables should only be serialized via SchemaDataItemProvider (structure only, no data). Explicitly block `AccessUser` data serialization. |
| YAML deserialization of uploaded zip with malicious content | Zip-slip path traversal, oversized extraction, YAML bomb (deeply nested aliases) | Validate zip entries for path traversal (`..`), enforce size limits, limit YAML nesting depth. YamlDotNet is safe against type-based attacks by default. |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Moving admin tree node from Settings > Content > Sync to Settings > Database > Serialize with no redirect | Users who bookmarked the old URL get blank page; muscle memory broken | Keep old tree node as a deprecation redirect for one major version. Show "Moved to Settings > Database > Serialize" banner. |
| Showing all 74 SQL tables as a flat checkbox list | Overwhelming, impossible to find specific tables | Mirror the DataGroup XML hierarchy in the UI: Ecommerce > Orders > Order Flows. Use collapsible tree with select-all per group. |
| Removing scheduled tasks without migration path | Users' nightly automated serialize/deserialize stops working on upgrade | Deprecate with warning in v2.0, keep functional. Remove in v3.0 only. Ship documentation showing API command equivalents for CI/CD. |
| Deserialization showing only "Processing..." spinner for minutes | Users think operation is hung, may close browser or trigger another run | Show per-table progress: "Deserializing EcomPayments (3/15 tables)... 47 rows processed" |
| No confirmation before destructive deserialization | Users accidentally overwrite production ecommerce settings | Show summary before execution: "This will modify 7 tables in 3 DataGroups. 142 rows will be inserted, 38 updated." Require explicit confirmation. |
| Settings deserialization with no diff preview | Users cannot see what will change before committing | Show before/after diff for settings keys, highlighting environment-specific values that will be skipped |

## "Looks Done But Isn't" Checklist

- [ ] **SqlTableProvider NULL round-trip:** Verify that NULL, empty string, and default value columns all survive serialize/deserialize unchanged for EVERY table type (varchar, int, bit, money, datetime, uniqueidentifier)
- [ ] **FK ordering on empty database:** Deserialize a DataGroup with 3+ tables (e.g. OrderFlows: EcomOrderFlow + EcomOrderStates + EcomOrderStateRules) against an EMPTY target database. Not just a populated one.
- [ ] **Empty NameColumn tables:** Tables with `NameColumn=""` use correct fallback matching. Test with `EcomCountries`, `EcomOrderStateRules`, `EcomVatCountryRelations`. Verify row counts match source.
- [ ] **Cross-DataGroup duplicate table:** Deserialize both Payment and Shipping DataGroups (both contain `EcomMethodCountryRelation`). Verify no duplicate rows and no lost rows.
- [ ] **ServiceCache invalidation:** After deserializing ecommerce settings, verify DW admin UI shows updated data WITHOUT app restart.
- [ ] **Settings environment safety:** Deserialize settings to a different environment. Verify SMTP config, domain URLs, payment credentials, and scheduled task configs are NOT overwritten.
- [ ] **Config backward compatibility:** Install v2.0 over v1.3. Verify existing config file loads correctly with all predicates intact. No manual migration required.
- [ ] **Content provider regression:** After provider architecture wrapping, full content tree round-trip (serialize from Swift 2.2, deserialize into Swift 2.1) produces byte-identical YAML output to v1.3.
- [ ] **Schema provider idempotency:** Run schema deserialization twice. Verify no failures, no duplicate columns, no schema drift.
- [ ] **ID mapping across tables:** Deserialize parent table (EcomPayments) and child table (EcomMethodCountryRelation). Verify child FK values correctly reference target parent IDs, not source parent IDs.
- [ ] **CompareColumns scope:** For tables with populated CompareColumns (e.g. EcomPayments with 20+ columns listed), verify only listed columns are serialized. For tables with empty CompareColumns, verify ALL columns are serialized.
- [ ] **Settings KeyPatterns as prefixes:** Verify that serializing `/Globalsettings/Ecom/Unit` captures all sub-keys (`Unit/Weight`, `Unit/Volume`, etc.), not just the prefix key itself.

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| FK constraint violations | LOW | Fix table ordering, re-run deserialization. Failed inserts are rolled back by transaction. No data corruption. |
| Identity column ID collision / wrong FK references | HIGH | Manual DB investigation to identify mismatched rows. Delete incorrectly matched rows, rebuild ID mapping, re-deserialize. May require source environment access. |
| ContentProvider regression after refactoring | MEDIUM | Revert provider wrapping commit. ContentSerializer/ContentDeserializer still work standalone. Re-attempt with smaller adapter surface area. |
| Environment-specific settings overwritten | HIGH | Restore settings from backup or re-enter manually. No automated recovery for payment gateway credentials or SMTP configs. Prevention is the only viable strategy. |
| Project rename breaking existing installations | MEDIUM | Ship hotfix with backward-compatible type aliases. Add startup migration for config/log file paths. |
| NULL vs empty string data corruption | HIGH | Re-serialize from source with fixed NULL handling, re-deserialize to target. Requires source environment to still be accessible with original data. |
| Duplicate junction table rows | LOW | Delete duplicate rows from junction tables. Add unique constraints to prevent recurrence. Re-run deserialization with deduplication enabled. |
| Stale DW caches after deserialization | LOW | Restart DW application, or add ServiceCache invalidation to deserialization pipeline. No data loss -- just stale UI. |
| Scheduled tasks removed, user automation broken | LOW | Re-add deprecated scheduled tasks in patch release. Provide API command documentation as migration path. |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| FK constraint ordering (P1) | Foundation (SqlTableProvider core) | Deserialize OrderFlows DataGroup (3 tables with FK deps) on empty DB -- all three tables succeed |
| Identity column handling (P2) | Foundation (SqlTableProvider core) | Deserialize EcomPayments, then EcomMethodCountryRelation. Verify child FK values reference correct target parent IDs |
| ContentProvider regression (P3) | Content migration (Wave 2) | All 10+ existing tests pass unchanged. Full Swift 2.2 -> Swift 2.1 round-trip matches v1.3 output |
| Empty NameColumn matching (P4) | Foundation (DataGroup XML parser) | Deserialize EcomCountries (no NameColumn) -- all rows survive round-trip with correct data |
| Environment-specific settings (P5) | Settings phase (Wave 4) | Deserialize settings to different env. SMTP, payment creds, domain URLs confirmed unchanged |
| Project rename breakage (P6) | Rename phase (first or last, never middle) | Upgrade v1.3 install to v2.0. Admin UI loads, config loads, scheduled tasks still resolve |
| NULL handling (P7) | Foundation (SqlTableProvider core) | Round-trip table with NULL, '', and default value columns. All three survive unchanged |
| Duplicate DataItemType (P8) | Foundation (DataGroup discovery) | Deserialize Payment + Shipping DataGroups. EcomMethodCountryRelation has no duplicates |
| Removing scheduled tasks (UX) | UX phase -- deprecation only in v2.0 | Deprecation warning logged on task execution. Tasks still functional in v2.0 |
| ServiceCache invalidation | Foundation (deserialization pipeline) | Deserialize ecommerce settings. DW admin UI reflects changes without restart |

## Sources

- Direct codebase analysis: `ContentSerializer.cs` (158 LOC), `ContentDeserializer.cs` (937 LOC), `SyncConfiguration.cs`, `SyncSettingsNodeProvider.cs` -- v1.3 implementation
- DataGroup XML inspection: `C:\temp\DataGroups\` -- ~100 XML files analyzed, including `DataGroup.xsd` schema
- Specific DataGroup XMLs examined for FK/identity/NameColumn patterns: `Settings_Ecommerce_Orders_060_OrderFlows.xml`, `Settings_Ecommerce_Orders_010_Payment.xml`, `Settings_Ecommerce_Orders_020_Shipping.xml`, `Settings_Ecommerce_Internationalization_010_Countries.xml`, `Settings_Ecommerce_Internationalization_030_Currencies.xml`, `Settings_Ecommerce_Internationalization_040_VATGroups.xml`, `Settings_System_010_SolutionSettings.xml`, `Settings_Ecommerce_AdvancedConfiguration_010_General.xml`, `Settings_ControlPanel_030_Users.xml`, `ECommerce.xml`
- DW API experience from v1.0-v1.3 development: GUID matching patterns, ItemService persistence quirks, SavePage/SaveParagraph DW overwrite behaviors, ServiceCaches usage
- `project_v2_pivot.md` memory file: wave plan, data group analysis, SqlTableProvider pattern
- PROJECT.md: v2.0 requirements, constraints, current architecture

---
*Pitfalls research for: DynamicWeb.Serializer v2.0 -- provider architecture, SQL table serialization, settings/schema providers*
*Researched: 2026-03-23*
