---
phase: 06-sync-robustness
plan: 01
subsystem: serialization
tags: [yaml, multi-column, grid, round-trip, backward-compat]

# Dependency graph
requires:
  - phase: 01-foundation
    provides: FileSystemStore write/read infrastructure and YAML serialization
  - phase: 03-serialization
    provides: ContentMapper paragraph mapping
provides:
  - ColumnId on SerializedParagraph for multi-column attribution
  - Column-aware paragraph filenames (paragraph-c{ColumnId}-{SortOrder}.yml)
  - ColumnId-based ReconstructColumns for accurate read-back
affects: [04-deserialization, 05-integration]

# Tech tracking
tech-stack:
  added: []
  patterns: [column-aware-filenames, columnid-based-reconstruction, backward-compat-null-default]

key-files:
  created: []
  modified:
    - src/Dynamicweb.ContentSync/Models/SerializedParagraph.cs
    - src/Dynamicweb.ContentSync/Infrastructure/FileSystemStore.cs
    - src/Dynamicweb.ContentSync/Serialization/ContentMapper.cs
    - tests/Dynamicweb.ContentSync.Tests/Fixtures/ContentTreeBuilder.cs
    - tests/Dynamicweb.ContentSync.Tests/Infrastructure/FileSystemStoreTests.cs

key-decisions:
  - "Column-aware filenames paragraph-c{ColumnId}-{SortOrder}.yml prevent SortOrder collisions across columns"
  - "ColumnId is int? (nullable) for backward compatibility - null defaults to first column on read-back"
  - "ColumnId stamped both in ContentMapper.BuildColumns (DW->DTO) and FileSystemStore.WritePage (DTO->disk)"

patterns-established:
  - "Column-aware filenames: paragraph-c{ColumnId}-{SortOrder}.yml for multi-column grid rows"
  - "Backward-compat null default: legacy files without ColumnId route to first column"

requirements-completed: [SER-01]

# Metrics
duration: 3min
completed: 2026-03-20
---

# Phase 06 Plan 01: Multi-Column Paragraph Round-Trip Summary

**ColumnId-based paragraph attribution with column-aware filenames for lossless multi-column grid round-trip**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-20T13:20:48Z
- **Completed:** 2026-03-20T13:24:11Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- Added ColumnId property to SerializedParagraph for column attribution metadata
- FileSystemStore writes column-aware filenames (paragraph-c{ColumnId}-{SortOrder}.yml) preventing SortOrder collisions
- ReconstructColumns distributes paragraphs to correct columns by ColumnId on read-back
- ContentMapper.BuildColumns stamps ColumnId when mapping from DW Paragraph objects
- Backward compatibility: legacy paragraph files (no ColumnId) default to column 1
- Full test suite green: 69 tests pass including 3 new multi-column tests

## Task Commits

Each task was committed atomically:

1. **Task 1 (RED): Add failing tests for multi-column round-trip** - `d2ebb28` (test)
2. **Task 1 (GREEN): Implement multi-column paragraph round-trip** - `1b9768c` (feat)

Task 2 deliverables (fixture, tests, updated assertions) were created as part of Task 1's TDD cycle.

## Files Created/Modified
- `src/Dynamicweb.ContentSync/Models/SerializedParagraph.cs` - Added ColumnId property (int?)
- `src/Dynamicweb.ContentSync/Infrastructure/FileSystemStore.cs` - Column-aware filenames in WritePage, ColumnId-based ReconstructColumns
- `src/Dynamicweb.ContentSync/Serialization/ContentMapper.cs` - Stamps ColumnId in BuildColumns
- `tests/Dynamicweb.ContentSync.Tests/Fixtures/ContentTreeBuilder.cs` - Added BuildMultiColumnTree fixture
- `tests/Dynamicweb.ContentSync.Tests/Infrastructure/FileSystemStoreTests.cs` - 3 new tests + updated existing filename assertion

## Decisions Made
- Column-aware filenames `paragraph-c{ColumnId}-{SortOrder}.yml` prevent SortOrder collisions across columns
- ColumnId is `int?` (nullable) for backward compatibility - null defaults to first column on read-back
- ColumnId stamped both in ContentMapper.BuildColumns (DW to DTO) and FileSystemStore.WritePage (DTO to disk)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Multi-column paragraph round-trip is now lossless
- SER-01 gap from v1.0 audit is closed
- Ready for phase 06 plan 02

---
*Phase: 06-sync-robustness*
*Completed: 2026-03-20*
