---
phase: 01-foundation
plan: "02"
subsystem: infra
tags: [yamldotnet, dotnet, csharp, filesystem, yaml, serialization, mirror-tree, xunit]

# Dependency graph
requires:
  - phase: 01-01
    provides: "DTOs (SerializedArea/Page/GridRow/GridColumn/Paragraph), YamlConfiguration factory, ContentTreeBuilder fixture"
provides:
  - IContentStore interface with WriteTree and ReadTree contract
  - FileSystemStore implementing mirror-tree file I/O (area/page/grid-row/paragraph hierarchy on disk)
  - Folder name sanitization replacing invalid chars while preserving spaces
  - Duplicate sibling page name disambiguation with short GUID suffix [xxxxxx]
  - Deterministic serialization: items sorted by SortOrder, dictionary keys sorted alphabetically
  - OmitEmptyCollections serializer so page.yml/area.yml don't include empty child-collection keys
  - 13 tests covering layout, sanitization, dedup, idempotency, round-trip read-back, field values, dictionary key order
affects:
  - Phase 2 (Configuration — can now read/write content trees from configurable paths)
  - Phase 3 (DW serialization — FileSystemStore is the persistence layer after mapping DW API objects to DTOs)
  - Phase 4 (Deserialization — ReadTree is the input to the write-back pipeline)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "FileSystemStore: mirror-tree path building using SanitizeFolderName + GetPageFolderName for dedup"
    - "Two serializers in FileSystemStore: _serializer (base) and _fileSerializer (OmitEmptyCollections) for per-item file writes"
    - "Pre-serialization Fields sort: SortFields() applies OrderBy(kv => kv.Key) before passing to serializer"
    - "Use real temp directories (Path.GetTempPath) in tests — not mocked file systems"
    - "IDisposable test class pattern for temp directory cleanup"

key-files:
  created:
    - "src/Dynamicweb.ContentSync/Infrastructure/IContentStore.cs"
    - "src/Dynamicweb.ContentSync/Infrastructure/FileSystemStore.cs"
    - "tests/Dynamicweb.ContentSync.Tests/Infrastructure/FileSystemStoreTests.cs"
  modified: []

key-decisions:
  - "Use OmitEmptyCollections on the file-level serializer (not the shared YamlConfiguration one) so page.yml and area.yml omit empty gridRows/pages keys without affecting other serializer consumers"
  - "Paragraphs written flat in grid-row folder (one file per paragraph across all columns); ReadTree restores all paragraphs to first column — multi-column attribution is out of scope for Phase 1"
  - "GridColumn metadata serialized inline in grid-row.yml (no separate column file per CONTEXT.md decision)"

patterns-established:
  - "Mirror-tree: SanitizeFolderName strips Path.GetInvalidFileNameChars, preserves spaces, returns _unnamed for empty"
  - "Dedup: GetPageFolderName checks usedNames HashSet (case-insensitive), appends [guid6] suffix on collision"
  - "Determinism: OrderBy(SortOrder).ThenBy(Name) for pages; OrderBy(SortOrder) for grid rows and paragraphs"

requirements-completed:
  - SER-02
  - SER-04

# Metrics
duration: 4min
completed: 2026-03-19
---

# Phase 1 Plan 02: FileSystemStore — Mirror-Tree File I/O Summary

**FileSystemStore with mirror-tree write/read: area/page/grid-row/paragraph YAML hierarchy, deterministic SortOrder ordering, GUID-suffix sibling dedup, and 13 passing tests proving all behaviors**

## Performance

- **Duration:** ~4 minutes
- **Started:** 2026-03-19T13:08:00Z
- **Completed:** 2026-03-19T13:12:03Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments

- IContentStore interface defining WriteTree/ReadTree contract
- FileSystemStore implementing the mirror-tree folder layout: area folder > page folders > grid-row-N folders > paragraph-N.yml files
- Folder name sanitization, spaces preserved, invalid chars replaced with underscore
- Duplicate sibling page names get `[xxxxxx]` GUID suffix (case-insensitive HashSet tracking)
- Deterministic output: pages sorted by SortOrder+Name, grid rows by SortOrder, paragraphs by SortOrder, Fields keys sorted alphabetically
- ReadTree reconstructs full content tree from disk, including field values with round-trip fidelity
- 13 xunit tests proving all required behaviors, all 28 solution-wide tests pass

## Task Commits

Each task was committed atomically:

1. **Task 1: IContentStore interface and FileSystemStore implementation** - `bbd9768` (feat)
2. **Task 2: FileSystemStore tests + OmitEmptyCollections fix** - `6e0a5bf` (test)

## Files Created/Modified

- `src/Dynamicweb.ContentSync/Infrastructure/IContentStore.cs` - Interface: WriteTree(SerializedArea, string) + ReadTree(string) -> SerializedArea
- `src/Dynamicweb.ContentSync/Infrastructure/FileSystemStore.cs` - Mirror-tree write/read, path sanitization, dedup, deterministic ordering, path length safety
- `tests/Dynamicweb.ContentSync.Tests/Infrastructure/FileSystemStoreTests.cs` - 13 tests: area/page/gridrow/paragraph layout, sanitization, spaces preserved, dedup, idempotency, SortOrder, round-trip, field values, dictionary key order

## Decisions Made

- Used a separate `_fileSerializer` with `OmitEmptyCollections` for writing per-item YAML files, rather than modifying `YamlConfiguration.BuildSerializer()`. This keeps the shared serializer unchanged for other consumers while preventing `gridRows: []` and `pages: []` from appearing in page.yml and area.yml.
- Paragraphs are written flat in the grid-row folder regardless of which column they belong to. On ReadTree, all paragraphs are reconstructed into the first column. Multi-column paragraph attribution (storing column ID per paragraph) is out of scope for Phase 1 and deferred.
- GridColumn metadata (Id, Width) is serialized inline in grid-row.yml with empty Paragraphs lists — no separate column file.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] OmitEmptyCollections needed for page.yml to not emit `gridRows: []`**
- **Found during:** Task 2 (WriteTree_PageYml_DoesNotContainGridRows test)
- **Issue:** `ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)` omits null values but NOT empty collections. The page record written with `GridRows = new List<>()` still serialized as `gridRows: []` in page.yml, violating the "page.yml contains only page metadata, not nested children" requirement.
- **Fix:** Added a private `_fileSerializer` using `OmitNull | OmitEmptyCollections`. Used this serializer for all per-item file writes (area.yml, page.yml, grid-row.yml). The shared `_serializer` from YamlConfiguration is unchanged.
- **Files modified:** `src/Dynamicweb.ContentSync/Infrastructure/FileSystemStore.cs`
- **Verification:** `WriteTree_PageYml_DoesNotContainGridRows` passes. All 28 tests pass.
- **Committed in:** `6e0a5bf` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - bug)
**Impact on plan:** Essential for correctness — the test directly enforces a locked architectural decision (one file per content item, children in subfolders). No scope creep.

## Issues Encountered

None beyond the deviation above.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- FileSystemStore is the persistence layer ready for Phase 3 (DW serialization) to call WriteTree after mapping DW API objects to DTOs
- IContentStore interface is the contract for dependency injection in Phase 2 (Configuration) and Phase 5 (Addin)
- ReadTree is the input for Phase 4 (Deserialization) — read YAML from disk, write to DW API
- Foundation layer (Plan 01 + Plan 02) is complete: DTOs, YAML config, mirror-tree I/O all proven

## Self-Check: PASSED

All 3 key files created and verified on disk. Both task commits (bbd9768, 6e0a5bf) confirmed in git log. All 28 tests pass.

---
*Phase: 01-foundation*
*Completed: 2026-03-19*
