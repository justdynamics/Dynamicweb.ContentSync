---
phase: 10-context-menu-actions
plan: 01
subsystem: ui
tags: [zip, download, serialize, context-menu, file-result]

# Dependency graph
requires:
  - phase: 07-config-infrastructure
    provides: ConfigLoader, ConfigWriter, ConfigPathResolver, SyncConfiguration
  - phase: 03-serialization
    provides: ContentSerializer pipeline
provides:
  - ExportDirectory config field in SyncConfiguration
  - SerializeSubtreeCommand returning FileResult for browser download
  - BuildContentPath utility for resolving page content paths
affects: [10-context-menu-actions plan 02 (injector wiring), 10-context-menu-actions plan 03 (deserialize)]

# Tech tracking
tech-stack:
  added: [System.IO.Compression (built-in)]
  patterns: [temp SyncConfiguration for ad-hoc serialize, FileResult for browser download, best-effort ExportDirectory copy]

key-files:
  created:
    - src/Dynamicweb.ContentSync/AdminUI/Commands/SerializeSubtreeCommand.cs
  modified:
    - src/Dynamicweb.ContentSync/Configuration/SyncConfiguration.cs
    - src/Dynamicweb.ContentSync/Configuration/ConfigLoader.cs
    - src/Dynamicweb.ContentSync/AdminUI/Models/SyncSettingsModel.cs
    - src/Dynamicweb.ContentSync/AdminUI/Commands/SaveSyncSettingsCommand.cs
    - src/Dynamicweb.ContentSync/AdminUI/Screens/SyncSettingsEditScreen.cs

key-decisions:
  - "ExportDirectory defaults to empty string (optional field, not required)"
  - "SerializeSubtreeCommand extends non-generic CommandBase (PageId/AreaId set by injector)"
  - "BuildContentPath walks parent chain to construct full content path for predicate matching"
  - "ExportDirectory copy is best-effort (failure does not block download)"

patterns-established:
  - "Temp SyncConfiguration with single predicate for ad-hoc page subtree operations"
  - "FileResult with FileStream (not MemoryStream) for zip downloads"

requirements-completed: [ACT-02, ACT-03, ACT-04, ACT-08]

# Metrics
duration: 2min
completed: 2026-03-22
---

# Phase 10 Plan 01: Serialize Subtree Command Summary

**ExportDirectory config field and SerializeSubtreeCommand that zips page subtree YAML via ContentSerializer reuse and returns FileResult for browser download**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-22T12:43:18Z
- **Completed:** 2026-03-22T12:45:01Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- Added ExportDirectory property through full config pipeline (SyncConfiguration, ConfigLoader, settings model, save command, edit screen)
- Created SerializeSubtreeCommand that reuses ContentSerializer with temp SyncConfiguration scoped to clicked page subtree
- Command produces zip with YAML mirror-tree layout plus export.log, returns FileResult for browser download
- Zip is also copied to ExportDirectory when configured (best-effort)

## Task Commits

Each task was committed atomically:

1. **Task 1: Add ExportDirectory to config pipeline** - `b9141ac` (feat)
2. **Task 2: Create SerializeSubtreeCommand** - `5bc38a8` (feat)

## Files Created/Modified
- `src/Dynamicweb.ContentSync/Configuration/SyncConfiguration.cs` - Added ExportDirectory property
- `src/Dynamicweb.ContentSync/Configuration/ConfigLoader.cs` - Added ExportDirectory to RawSyncConfiguration and Load method
- `src/Dynamicweb.ContentSync/AdminUI/Models/SyncSettingsModel.cs` - Added ExportDirectory field with ConfigurableProperty
- `src/Dynamicweb.ContentSync/AdminUI/Commands/SaveSyncSettingsCommand.cs` - Wired ExportDirectory into config save
- `src/Dynamicweb.ContentSync/AdminUI/Screens/SyncSettingsEditScreen.cs` - Added ExportDirectory editor
- `src/Dynamicweb.ContentSync/AdminUI/Commands/SerializeSubtreeCommand.cs` - New command for subtree serialize-to-zip-and-download

## Decisions Made
- ExportDirectory defaults to empty string -- optional field, empty means no export copy
- SerializeSubtreeCommand uses non-generic CommandBase (no model binding needed)
- BuildContentPath walks page.ParentPageId chain to construct full content path matching ContentSerializer's path format
- ExportDirectory copy is best-effort -- failure logged but does not block the download

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- Build has a pre-existing error in DeserializePromptQuery.cs (from parallel agent) referencing QueryBase<> which is not yet available. This is not caused by this plan's changes -- our files compile cleanly (0 errors excluding the pre-existing file).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- SerializeSubtreeCommand is ready for wiring into the ListScreenInjector (plan 02)
- ExportDirectory available in config for runtime use

---
*Phase: 10-context-menu-actions*
*Completed: 2026-03-22*
