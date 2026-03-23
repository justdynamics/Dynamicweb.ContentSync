---
phase: 13-provider-foundation-sqltableprovider-proof
plan: 02
subsystem: providers
tags: [sqltable, serialization, yaml, xml-parsing, identity-resolution, md5-checksum]

# Dependency graph
requires:
  - phase: 13-01
    provides: ISerializationProvider interface, SerializationProviderBase, ISqlExecutor, TableMetadata, ProviderPredicateDefinition
provides:
  - DataGroupMetadataReader parsing DataGroup XML to TableMetadata via System.Xml.Linq
  - SqlTableReader reading SQL rows with DBNull-to-null mapping and identity resolution
  - FlatFileStore writing per-row YAML to _sql/{TableName}/ layout with _meta.yml
  - SqlTableProvider.Serialize end-to-end pipeline (metadata + read + write)
  - YAML null round-trip contract (C# null -> ~ -> C# null)
affects: [13-03, 14-content-provider-adapter]

# Tech tracking
tech-stack:
  added: [System.Xml.Linq, System.Security.Cryptography.MD5]
  patterns: [datagroup-xml-parsing, identity-resolution, flat-file-yaml-store, sql-null-yaml-fidelity]

key-files:
  created:
    - src/Dynamicweb.ContentSync/Providers/SqlTable/DataGroupMetadataReader.cs
    - src/Dynamicweb.ContentSync/Providers/SqlTable/SqlTableReader.cs
    - src/Dynamicweb.ContentSync/Providers/SqlTable/FlatFileStore.cs
    - src/Dynamicweb.ContentSync/Providers/SqlTable/SqlTableProvider.cs
    - tests/Dynamicweb.ContentSync.Tests/Fixtures/DataGroups/TestOrderFlows.xml
    - tests/Dynamicweb.ContentSync.Tests/Providers/SqlTable/DataGroupMetadataReaderTests.cs
    - tests/Dynamicweb.ContentSync.Tests/Providers/SqlTable/SqlTableReaderTests.cs
    - tests/Dynamicweb.ContentSync.Tests/Providers/SqlTable/FlatFileStoreTests.cs
    - tests/Dynamicweb.ContentSync.Tests/Providers/SqlTable/IdentityResolutionTests.cs
  modified: []

key-decisions:
  - "Dictionary keys preserved as-is in YAML (not camelCased) since YamlDotNet naming convention only applies to object properties"
  - "FlatFileStore uses DefaultValuesHandling.Preserve to keep null values in serialized YAML output"
  - "DataGroupMetadataReader caches all parsed XML files to avoid re-parsing on multiple calls"

patterns-established:
  - "DataGroup XML parsing: enumerate *.xml files, find DataGroup by Id, extract SqlDataItemProvider parameters"
  - "Identity resolution: NameColumn value when available, composite PK with $$ separator when not (alphabetical sort)"
  - "SQL null fidelity: separate YAML serializer with Preserve handling, YAML empty value round-trips to C# null"

requirements-completed: [SQL-01, SQL-02]

# Metrics
duration: 4min
completed: 2026-03-23
---

# Phase 13 Plan 02: SqlTableProvider Serialization Pipeline Summary

**SQL table to YAML serialization: DataGroup XML metadata parsing, row reading with identity resolution, and per-row YAML files in _sql/{TableName}/ layout with null round-trip fidelity**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-23T19:22:10Z
- **Completed:** 2026-03-23T19:26:35Z
- **Tasks:** 2
- **Files modified:** 10

## Accomplishments
- DataGroupMetadataReader parses DataGroup XML via System.Xml.Linq, queries PK/identity/all columns via ISqlExecutor
- SqlTableReader reads SQL rows with DBNull-to-null mapping, generates identity via NameColumn or composite PK with $$ separator, calculates MD5 checksums for change detection
- FlatFileStore writes per-row YAML files to _sql/{TableName}/ with _meta.yml and deduplication logic
- SqlTableProvider.Serialize orchestrates full pipeline: metadata -> read rows -> write YAML
- 15 new unit tests (26 total Phase13) covering metadata parsing, row reading, identity resolution, YAML null round-trip
- YAML null round-trip contract proven: C# null serializes to empty YAML value, deserializes back to C# null (not string)

## Task Commits

Each task was committed atomically:

1. **Task 1: DataGroupMetadataReader, SqlTableReader, and FlatFileStore** - `c10a07d` (feat)
2. **Task 2: SqlTableProvider.Serialize + unit tests** - `c10bd78` (feat)

## Files Created/Modified
- `src/Dynamicweb.ContentSync/Providers/SqlTable/DataGroupMetadataReader.cs` - Parses DataGroup XML to TableMetadata
- `src/Dynamicweb.ContentSync/Providers/SqlTable/SqlTableReader.cs` - Reads SQL rows, identity resolution, checksum
- `src/Dynamicweb.ContentSync/Providers/SqlTable/FlatFileStore.cs` - Per-row YAML I/O in _sql/ layout
- `src/Dynamicweb.ContentSync/Providers/SqlTable/SqlTableProvider.cs` - ISerializationProvider implementation (Serialize only)
- `tests/Dynamicweb.ContentSync.Tests/Fixtures/DataGroups/TestOrderFlows.xml` - EcomOrderFlow DataGroup fixture
- `tests/Dynamicweb.ContentSync.Tests/Providers/SqlTable/DataGroupMetadataReaderTests.cs` - 2 tests
- `tests/Dynamicweb.ContentSync.Tests/Providers/SqlTable/SqlTableReaderTests.cs` - 2 tests
- `tests/Dynamicweb.ContentSync.Tests/Providers/SqlTable/FlatFileStoreTests.cs` - 6 tests
- `tests/Dynamicweb.ContentSync.Tests/Providers/SqlTable/IdentityResolutionTests.cs` - 5 tests

## Decisions Made
- Dictionary keys preserved as-is in YAML (YamlDotNet naming convention only applies to object properties, not dictionary keys)
- FlatFileStore builds its own serializer with DefaultValuesHandling.Preserve for SQL NULL fidelity
- DataGroupMetadataReader caches parsed XML for efficiency on multi-table operations
- CommandBuilder uses inline string interpolation for table names (DW CommandBuilder lacks AddParam method)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] CommandBuilder.AddParam does not exist**
- **Found during:** Task 1 (DataGroupMetadataReader)
- **Issue:** Plan specified `cb.AddParam("table", tableName)` but DW's CommandBuilder has no AddParam method
- **Fix:** Used inline string interpolation in SQL query: `$"... WHERE TABLE_NAME = '{tableName}'"`
- **Files modified:** DataGroupMetadataReader.cs
- **Verification:** Build succeeds, tests pass
- **Committed in:** c10a07d (Task 1 commit)

**2. [Rule 1 - Bug] YAML null assertion expected lowercase key**
- **Found during:** Task 2 (FlatFileStoreTests)
- **Issue:** Test asserted `content.Contains("description: ")` but dictionary keys are PascalCase, not camelCase
- **Fix:** Changed assertion to `content.Contains("Description: ")` matching actual YAML output
- **Verification:** All 6 FlatFileStore tests pass
- **Committed in:** c10bd78 (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (2 bugs)
**Impact on plan:** Both fixes necessary for correctness. No scope creep.

## Issues Encountered
None beyond the auto-fixed deviations.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- SqlTableProvider.Serialize works end-to-end with mocked ISqlExecutor
- Deserialize method is a stub (NotImplementedException) ready for Plan 03
- FlatFileStore.ReadAllRows and ReadMeta ready for deserialization pipeline
- YAML null round-trip contract proven for Plan 03's SqlTableWriter (null -> DBNull.Value mapping)

## Known Stubs
- `SqlTableProvider.Deserialize` throws `NotImplementedException` - intentional, implemented in Plan 13-03

---
*Phase: 13-provider-foundation-sqltableprovider-proof*
*Completed: 2026-03-23*
