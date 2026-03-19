---
phase: 03-serialization
plan: 02
subsystem: serialization
tags: [csharp, dotnet, dynamicweb, mapper, serializer, yaml, reference-resolution]

# Dependency graph
requires:
  - phase: 03-serialization
    plan: 01
    provides: SerializedPage.Children, FileSystemStore recursive write/read
  - phase: 02-configuration
    plan: 01
    provides: ContentPredicateSet, SyncConfiguration, PredicateDefinition
  - phase: 01-foundation
    plan: 01
    provides: FileSystemStore, SerializedArea, SerializedPage, SerializedGridRow, SerializedGridColumn, SerializedParagraph

provides:
  - ContentMapper: maps DW Area/Page/GridRow/Paragraph objects to existing DTO records
  - ReferenceResolver: resolves numeric page/paragraph IDs to GUIDs with caching
  - ContentSerializer: orchestrates DW API traversal, predicate filtering, mapping, and FileSystemStore output
  - DW DLL references (Dynamicweb.dll, Dynamicweb.Core.dll) from Swift2.1 bin

affects:
  - 03-serialization integration tests (subsequent plans)
  - 04-deserialization (ContentSerializer produces the YAML trees ReadTree consumes)

# Tech tracking
tech-stack:
  added:
    - "Dynamicweb.dll (DLL reference) — Dynamicweb.Content namespace: PageService, AreaService, GridService, ParagraphService, Area, Page, GridRow, Paragraph, Services static accessor"
    - "Dynamicweb.Core.dll (DLL reference) — dependency of Dynamicweb.dll"
  patterns:
    - "DLL reference approach: Dynamicweb.Core NuGet lacks Dynamicweb.Content; DLLs copied from Swift2.1 (net8.0) bin"
    - "Services.Xxx static accessor pattern for DW content service resolution (no DI container)"
    - "ReferenceResolver cache: paragraph GUIDs registered during traversal to resolve forward references"
    - "ContentSerializer short-circuits predicate check before loading children (perf optimization)"
    - "MasterParagraphID and GlobalRecordPageID serialized as GUID strings, not raw int IDs"

key-files:
  created:
    - src/Dynamicweb.ContentSync/Serialization/ContentMapper.cs
    - src/Dynamicweb.ContentSync/Serialization/ReferenceResolver.cs
    - src/Dynamicweb.ContentSync/Serialization/ContentSerializer.cs
    - lib/Dynamicweb.dll
    - lib/Dynamicweb.Core.dll
  modified:
    - src/Dynamicweb.ContentSync/Dynamicweb.ContentSync.csproj
    - .gitignore

key-decisions:
  - "DLL reference over NuGet for Dynamicweb.Content: Dynamicweb.Core NuGet (10.23.9) does not include Dynamicweb.Content namespace — only AI, Auditing, Caching utilities. The content API (PageService, AreaService, etc.) lives in Dynamicweb.dll. DLLs copied from Swift2.1 (net8.0) bin as the plan's documented fallback."
  - "Private=false on DLL references: ContentSync DLL will be deployed to DW instance bin folder where Dynamicweb.dll is already present; no need to copy it alongside ContentSync.dll"
  - "Services.Xxx static accessor pattern chosen over new XxxService() for consistency with DW10 canonical style"

patterns-established:
  - "Predicate filter applied BEFORE child page loading to short-circuit subtree traversal"
  - "BuildColumns groups paragraphs by GridRowColumn; returns single empty column when page has no paragraphs"
  - "ReferenceResolver.RegisterParagraph called during MapParagraph so later cross-references resolve correctly"

requirements-completed: [SER-03, INF-02]

# Metrics
duration: 5min
completed: 2026-03-19
---

# Phase 3 Plan 02: DW-to-DTO Serialization Pipeline Summary

**JWT-style GUID-only DW content serialization via ContentMapper, ReferenceResolver, and ContentSerializer connecting live DW content APIs to the existing FileSystemStore/YAML infrastructure**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-19T17:50:09Z
- **Completed:** 2026-03-19T17:55:00Z
- **Tasks:** 2
- **Files modified:** 7 (3 created in Serialization/, 2 DLLs in lib/, csproj, .gitignore)

## Accomplishments

- Added DLL references for `Dynamicweb.dll` (Dynamicweb.Content namespace) and `Dynamicweb.Core.dll` copied from Swift2.1 (net8.0) bin — replaces the Dynamicweb.Core NuGet which lacks the Content API
- Created `ReferenceResolver` with page/paragraph GUID caches, `RegisterParagraph` for traversal-time registration, `ResolvePageGuid` via Services.Pages.GetPage, and `Clear()` for between-run cleanup
- Created `ContentMapper` with `MapArea`, `MapPage`, `MapGridRow`, `MapParagraph`, `BuildColumns` methods — maps all four DW content types to existing DTOs; resolves `MasterParagraphID` and `GlobalRecordPageID` to GUIDs
- Created `ContentSerializer` orchestrating the full pipeline: iterates predicates, fetches area via `Services.Areas.GetArea`, finds root pages via `Services.Pages.GetRootPagesForArea`, recursively traverses via `GetPagesByParentID`, applies `ContentPredicateSet.ShouldInclude`, maps to DTOs, writes via `FileSystemStore.WriteTree`
- All 60 existing unit tests continue to pass

## Task Commits

Each task was committed atomically:

1. **Task 1: Add DW DLL references and create ContentMapper + ReferenceResolver** - `b664244` (feat)
2. **Task 2: Create ContentSerializer orchestrator** - `a06e139` (feat)

## Files Created/Modified

- `src/Dynamicweb.ContentSync/Serialization/ContentMapper.cs` — maps Area/Page/GridRow/Paragraph DW objects to DTOs
- `src/Dynamicweb.ContentSync/Serialization/ReferenceResolver.cs` — numeric ID to GUID resolution with caching
- `src/Dynamicweb.ContentSync/Serialization/ContentSerializer.cs` — orchestrates DW traversal, filtering, mapping, FileSystemStore write
- `src/Dynamicweb.ContentSync/Dynamicweb.ContentSync.csproj` — replaced Dynamicweb.Core NuGet with DLL references
- `lib/Dynamicweb.dll` — DW content API (PageService, AreaService, GridService, ParagraphService)
- `lib/Dynamicweb.Core.dll` — DW core utilities (dependency of Dynamicweb.dll)
- `.gitignore` — added .claude/ exclusion

## Decisions Made

- **DLL reference over NuGet:** `Dynamicweb.Core` NuGet 10.23.9 only contains AI, Auditing, Caching namespaces — not `Dynamicweb.Content`. The plan's documented fallback (DLL copy from Swift2.1 bin) was used. DLLs marked `Private=false` so they are not copied alongside ContentSync.dll during deployment.
- **Services.Xxx pattern:** Used canonical DW10 static accessor (`Services.Pages`, `Services.Areas`, etc.) rather than `new XxxService()` — both work but the static accessor is the documented modern approach.
- **Predicate short-circuit:** `ContentPredicateSet.ShouldInclude` is checked before fetching grid rows and children, avoiding unnecessary DW API calls for excluded pages.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Dynamicweb.Core NuGet does not contain Dynamicweb.Content namespace**
- **Found during:** Task 1 (first build attempt)
- **Issue:** `Dynamicweb.Core` NuGet 10.23.9 restores successfully but provides no `Dynamicweb.Content` types — the package only includes AI/Auditing/Caching utilities. Build failed with `CS0234: The type or namespace name 'Content' does not exist in the namespace 'Dynamicweb'`.
- **Fix:** Applied the plan's documented fallback: copied `Dynamicweb.dll` and `Dynamicweb.Core.dll` from Swift2.1 (net8.0) bin to `lib/` and replaced the NuGet `PackageReference` with `<Reference>` elements pointing to the DLL HintPaths.
- **Files modified:** `Dynamicweb.ContentSync.csproj`, new `lib/Dynamicweb.dll`, `lib/Dynamicweb.Core.dll`
- **Commit:** `b664244` (included in Task 1 commit)

**Total deviations:** 1 auto-fixed (Rule 3 - Blocking issue, handled via plan's documented fallback path)
**Impact on plan:** None — the plan explicitly described this fallback. All acceptance criteria met.

## Issues Encountered

- The Dynamicweb.Core NuGet package does not include `Dynamicweb.Content` — this is a known gap documented in the plan's fallback instructions. Resolved via DLL reference approach.

## User Setup Required

None for this plan. Future integration test setup will require a running Swift2.2 instance and DLL deployment (documented in 03-RESEARCH.md).

## Next Phase Readiness

- ContentMapper correctly maps all four DW content types (Area, Page, GridRow, Paragraph) to existing DTOs
- ReferenceResolver prevents numeric IDs from leaking into serialized output
- ContentSerializer is ready to connect to a running DW instance for integration testing
- FileSystemStore.WriteTree (from Phase 1) handles recursive page hierarchy — children populated by ContentSerializer will be written as nested subfolders

---
*Phase: 03-serialization*
*Completed: 2026-03-19*
