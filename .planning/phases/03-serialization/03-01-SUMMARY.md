---
phase: 03-serialization
plan: 01
subsystem: infra
tags: [yaml, filesystem, serialization, csharp, dotnet]

# Dependency graph
requires:
  - phase: 01-foundation
    provides: FileSystemStore, SerializedPage, SerializedArea, YamlConfiguration

provides:
  - SerializedPage.Children property enabling recursive page hierarchies
  - FileSystemStore.WritePage private helper for recursive child page writing
  - FileSystemStore.ReadPage private helper for recursive child page reading
  - BuildNestedTree() test fixture for 3-level page hierarchy
  - INF-03 long-path safety tests covering truncation, deep nesting, and no-crash guarantees
  - Bug fix: SafeGetDirectory negative maxFolderLength when parent path >= 247 chars

affects:
  - 03-serialization (subsequent plans using FileSystemStore)
  - 04-deserialization (ReadTree now returns Children)
  - DW mapper (will produce pages with Children populated)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Recursive page folder nesting: children written as subfolders of parent page directory"
    - "Per-level sibling deduplication: new HashSet per parent, not global"
    - "Children omitted from page.yml YAML via OmitEmptyCollections; presence detected by subfolder page.yml existence"

key-files:
  created: []
  modified:
    - src/Dynamicweb.ContentSync/Models/SerializedPage.cs
    - src/Dynamicweb.ContentSync/Infrastructure/FileSystemStore.cs
    - tests/Dynamicweb.ContentSync.Tests/Fixtures/ContentTreeBuilder.cs
    - tests/Dynamicweb.ContentSync.Tests/Infrastructure/FileSystemStoreTests.cs

key-decisions:
  - "Children stored as nested subfolders rather than YAML arrays — keeps page.yml diff-friendly and consistent with area/page structure"
  - "Per-level usedNames HashSet for sibling deduplication — siblings of the same parent share a namespace, not a global one"
  - "SafeGetDirectory falls back to 8-char GUID folder name when parent path itself exceeds 247 chars — prevents ArgumentOutOfRangeException on extreme nesting"

patterns-established:
  - "ReadPage detects child pages by presence of page.yml in subdir (not grid-row.yml) — distinguishes children from grid row folders cleanly"
  - "WritePage recursion: children written after grid rows, using fresh usedChildNames per parent"

requirements-completed: [INF-03]

# Metrics
duration: 8min
completed: 2026-03-19
---

# Phase 3 Plan 01: Recursive Page Hierarchy and Long-Path Safety Summary

**Recursive multi-level page tree serialization via nested folder structure, with INF-03 long-path safety tests and a SafeGetDirectory overflow bug fix**

## Performance

- **Duration:** 8 min
- **Started:** 2026-03-19T17:43:46Z
- **Completed:** 2026-03-19T17:51:00Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- Added `List<SerializedPage> Children` to SerializedPage, enabling recursive page hierarchies
- Refactored WriteTree into private `WritePage` helper that recursively writes child pages as nested subfolders, omitting Children from page.yml YAML
- Refactored ReadTree into private `ReadPage` helper that detects child page subfolders (by presence of page.yml) and recursively reconstructs the hierarchy
- Added `BuildNestedTree()` test fixture building a 3-level hierarchy (Area > Parent > Child A/Child B > Grandchild + Sibling)
- Added 4 nested page tests: folder creation, YAML omission, hierarchy reconstruction, round-trip fidelity
- Added 3 INF-03 long-path tests: path truncation with warning, deeply nested children, no-crash guarantee

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Children to SerializedPage and recursive page tree support** - `da3496e` (feat)
2. **Task 2: Add INF-03 long-path tests and fix SafeGetDirectory overflow bug** - `29ae4ee` (feat)

## Files Created/Modified
- `src/Dynamicweb.ContentSync/Models/SerializedPage.cs` - Added `List<SerializedPage> Children { get; init; } = new()`
- `src/Dynamicweb.ContentSync/Infrastructure/FileSystemStore.cs` - Extracted WritePage/ReadPage helpers; fixed SafeGetDirectory negative-length bug
- `tests/Dynamicweb.ContentSync.Tests/Fixtures/ContentTreeBuilder.cs` - Added BuildNestedTree() 3-level fixture
- `tests/Dynamicweb.ContentSync.Tests/Infrastructure/FileSystemStoreTests.cs` - Added 7 new tests (4 recursive + 3 INF-03)

## Decisions Made
- Children stored as nested subfolders rather than YAML arrays — keeps page.yml diff-friendly and consistent with area/page/paragraph folder structure
- Per-level usedNames HashSet for sibling deduplication — siblings of the same parent share a namespace, not global across all pages
- ReadPage distinguishes child page subfolders (page.yml present) from grid-row subfolders (grid-row.yml present) — clean separation using existing file markers

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed SafeGetDirectory crash when parent path >= 247 chars**
- **Found during:** Task 2 (WriteTree_DeeplyNestedChildren_HandlesLongPaths test)
- **Issue:** `maxFolderLength = 247 - parentDirectory.Length - 1` went negative when parent exceeded 247 chars, causing `ArgumentOutOfRangeException` in `String.Substring`
- **Fix:** Added guard: when `maxFolderLength <= suffix.Length`, fall back to an 8-char GUID folder name instead of crashing
- **Files modified:** `src/Dynamicweb.ContentSync/Infrastructure/FileSystemStore.cs`
- **Verification:** All 60 tests pass including the deeply-nested path test that triggered the crash
- **Committed in:** `29ae4ee` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - Bug)
**Impact on plan:** Necessary correctness fix — the long-path test revealed a latent crash. No scope creep.

## Issues Encountered
- The `-x` flag for `dotnet test` was not recognized by this dotnet version (10.0.103) — ran without it (no impact on test execution)

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- FileSystemStore now handles arbitrary-depth page hierarchies; ready for DW mapper to produce pages with Children
- ReadTree returns Children populated; 04-deserialization can consume multi-level trees without further store changes
- Long-path safety confirmed by explicit tests covering truncation, deep nesting, and extreme path lengths

---
*Phase: 03-serialization*
*Completed: 2026-03-19*
