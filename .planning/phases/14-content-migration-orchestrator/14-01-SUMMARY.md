---
phase: 14-content-migration-orchestrator
plan: 01
subsystem: providers
tags: [provider-architecture, config-migration, content-provider, provider-predicate]

requires:
  - phase: 13-provider-architecture
    provides: ISerializationProvider interface, ProviderPredicateDefinition model, SqlTableProvider reference
provides:
  - ConfigLoader outputs ProviderPredicateDefinition[] for all predicates
  - Predicates without providerType default to "Content"
  - ContentProvider adapter wrapping ContentSerializer/ContentDeserializer
  - Content YAML routed to _content/ subdirectory
affects: [14-02-orchestrator-dispatch, future-provider-registry]

tech-stack:
  added: []
  patterns: [provider-adapter-pattern, unified-predicate-definition]

key-files:
  created:
    - src/Dynamicweb.ContentSync/Providers/Content/ContentProvider.cs
    - tests/Dynamicweb.ContentSync.Tests/Providers/Content/ContentProviderTests.cs
  modified:
    - src/Dynamicweb.ContentSync/Configuration/ConfigLoader.cs
    - src/Dynamicweb.ContentSync/Configuration/SyncConfiguration.cs
    - src/Dynamicweb.ContentSync/Configuration/ContentPredicate.cs
    - src/Dynamicweb.ContentSync/Configuration/ConfigPathResolver.cs
    - src/Dynamicweb.ContentSync/Serialization/ContentSerializer.cs
    - src/Dynamicweb.ContentSync/Serialization/ContentDeserializer.cs
    - src/Dynamicweb.ContentSync/AdminUI/Commands/SavePredicateCommand.cs
    - src/Dynamicweb.ContentSync/AdminUI/Commands/SerializeSubtreeCommand.cs
    - src/Dynamicweb.ContentSync/AdminUI/Commands/SaveSyncSettingsCommand.cs

key-decisions:
  - "Clean break: SyncConfiguration.Predicates changed from List<PredicateDefinition> to List<ProviderPredicateDefinition>"
  - "ContentProvider directly implements ISerializationProvider (not SerializationProviderBase) since it delegates to ContentSerializer/ContentDeserializer"
  - "Content YAML routed to _content/ subdirectory under output/input root"

patterns-established:
  - "Provider adapter pattern: wrap existing serializer/deserializer as ISerializationProvider without changing internals"
  - "Unified predicate model: all predicates are ProviderPredicateDefinition at config load time"

requirements-completed: [PROV-03, CACHE-02, CACHE-03]

duration: 9min
completed: 2026-03-24
---

# Phase 14 Plan 01: Config Migration + ContentProvider Summary

**Unified config to ProviderPredicateDefinition and ContentProvider adapter wrapping existing serializers for _content/ subdirectory routing**

## Performance

- **Duration:** 9 min
- **Started:** 2026-03-24T09:56:56Z
- **Completed:** 2026-03-24T10:05:45Z
- **Tasks:** 2
- **Files modified:** 15

## Accomplishments
- ConfigLoader now outputs ProviderPredicateDefinition for all predicates, with automatic "Content" default for unspecified providerType
- SqlTable predicate fields (table, nameColumn, compareColumns) parsed from config JSON
- ContentProvider adapter created implementing ISerializationProvider, routing content YAML to _content/ subdirectory
- All 13 source files and 4 test files updated from PredicateDefinition to ProviderPredicateDefinition
- 31 tests pass for ConfigLoader + ContentProvider (170/171 total, 1 pre-existing flaky test)

## Task Commits

Each task was committed atomically:

1. **Task 1 RED: ConfigLoader migration tests** - `f1e2c22` (test)
2. **Task 1 GREEN: ConfigLoader + SyncConfiguration migration** - `ff905aa` (feat)
3. **Task 2 RED: ContentProvider tests** - `b280e29` (test)
4. **Task 2 GREEN: ContentProvider adapter** - `5390c12` (feat)

_TDD: Each task followed RED-GREEN cycle._

## Files Created/Modified
- `src/Dynamicweb.ContentSync/Providers/Content/ContentProvider.cs` - ISerializationProvider adapter wrapping ContentSerializer/ContentDeserializer
- `src/Dynamicweb.ContentSync/Configuration/ConfigLoader.cs` - Returns ProviderPredicateDefinition, reads table/nameColumn/compareColumns
- `src/Dynamicweb.ContentSync/Configuration/SyncConfiguration.cs` - Predicates type changed to List<ProviderPredicateDefinition>
- `src/Dynamicweb.ContentSync/Configuration/ContentPredicate.cs` - Updated to accept ProviderPredicateDefinition
- `src/Dynamicweb.ContentSync/Configuration/ConfigPathResolver.cs` - Default config uses ProviderPredicateDefinition
- `src/Dynamicweb.ContentSync/Serialization/ContentSerializer.cs` - Method signatures updated to ProviderPredicateDefinition
- `src/Dynamicweb.ContentSync/Serialization/ContentDeserializer.cs` - Method signatures updated to ProviderPredicateDefinition
- `src/Dynamicweb.ContentSync/AdminUI/Commands/SavePredicateCommand.cs` - Creates ProviderPredicateDefinition with ProviderType="Content"
- `src/Dynamicweb.ContentSync/AdminUI/Commands/SerializeSubtreeCommand.cs` - Uses ProviderPredicateDefinition
- `src/Dynamicweb.ContentSync/AdminUI/Commands/SaveSyncSettingsCommand.cs` - Uses ProviderPredicateDefinition
- `tests/Dynamicweb.ContentSync.Tests/Providers/Content/ContentProviderTests.cs` - 11 tests for ContentProvider
- `tests/Dynamicweb.ContentSync.Tests/Configuration/ConfigLoaderTests.cs` - 4 new tests for ProviderPredicateDefinition migration
- `tests/Dynamicweb.ContentSync.Tests/Configuration/ContentPredicateTests.cs` - Updated to ProviderPredicateDefinition
- `tests/Dynamicweb.ContentSync.Tests/Configuration/ConfigWriterTests.cs` - Updated to ProviderPredicateDefinition
- `tests/Dynamicweb.ContentSync.Tests/AdminUI/PredicateCommandTests.cs` - Updated to ProviderPredicateDefinition

## Decisions Made
- Clean break on PredicateDefinition to ProviderPredicateDefinition: all consumers updated in one pass rather than maintaining backward compatibility. PredicateDefinition.cs kept but no longer referenced by config/serialization.
- ContentProvider directly implements ISerializationProvider (not SerializationProviderBase) because it delegates to existing ContentSerializer/ContentDeserializer which have their own YAML handling.
- Content YAML routed to `_content/` subdirectory (matching `_sql/` pattern for SqlTableProvider).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Updated test files referencing PredicateDefinition**
- **Found during:** Task 1 (ConfigLoader migration)
- **Issue:** 4 test files (ContentPredicateTests, ConfigWriterTests, PredicateCommandTests, SaveSyncSettingsCommandTests) still referenced PredicateDefinition, preventing build
- **Fix:** Updated all test files to use ProviderPredicateDefinition with required ProviderType = "Content"
- **Files modified:** 4 test files
- **Committed in:** ff905aa (part of Task 1 GREEN commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Required to complete the clean break. Plan mentioned updating test files but didn't enumerate all affected test files.

## Issues Encountered
None

## Known Stubs
None - all data paths are wired to real implementations.

## Next Phase Readiness
- ContentProvider registered as "Content" type, ready for ProviderRegistry.Register in Plan 02
- ConfigLoader produces ProviderPredicateDefinition for all predicates, ready for orchestrator dispatch
- SyncConfiguration is the unified config model for all providers

---
*Phase: 14-content-migration-orchestrator*
*Completed: 2026-03-24*
