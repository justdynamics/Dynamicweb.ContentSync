---
phase: 11-permission-serialization
plan: 01
subsystem: serialization
tags: [permissions, yaml, dto, dynamicweb]

# Dependency graph
requires:
  - phase: 03-serialization
    provides: ContentSerializer, ContentMapper, SerializedPage DTO, YAML pipeline
provides:
  - SerializedPermission DTO with Owner, OwnerType, OwnerId, Level, LevelValue
  - PermissionMapper class with IsRole, GetLevelName, MapPermissions
  - SerializedPage.Permissions list (empty-omitted from YAML)
  - ContentSerializer wiring that queries permissions for every page
affects: [12-permission-deserialization]

# Tech tracking
tech-stack:
  added: []
  patterns: [PermissionMapper as separate I/O class keeping ContentMapper pure]

key-files:
  created:
    - src/Dynamicweb.ContentSync/Models/SerializedPermission.cs
    - src/Dynamicweb.ContentSync/Serialization/PermissionMapper.cs
    - tests/Dynamicweb.ContentSync.Tests/Serialization/PermissionSerializationTests.cs
  modified:
    - src/Dynamicweb.ContentSync/Models/SerializedPage.cs
    - src/Dynamicweb.ContentSync/Serialization/ContentMapper.cs
    - src/Dynamicweb.ContentSync/Serialization/ContentSerializer.cs
    - tests/Dynamicweb.ContentSync.Tests/Fixtures/ContentTreeBuilder.cs
    - tests/Dynamicweb.ContentSync.Tests/Models/DtoTests.cs
    - tests/Dynamicweb.ContentSync.Tests/Infrastructure/YamlRoundTripTests.cs

key-decisions:
  - "PermissionMapper does I/O (PermissionService + UserManagementServices), ContentMapper stays pure"
  - "Used UserManagementServices.Users.GetUserById instead of deprecated User.GetUserByID"

patterns-established:
  - "Separate mapper classes for I/O-dependent mapping (PermissionMapper pattern)"

requirements-completed: [PERM-01, PERM-02, PERM-03]

# Metrics
duration: 7min
completed: 2026-03-22
---

# Phase 11 Plan 01: Permission Serialization Summary

**SerializedPermission DTO and PermissionMapper that reads DW PermissionService, resolves role/group names, and integrates into ContentSerializer pipeline with 17 passing tests**

## Performance

- **Duration:** 7 min
- **Started:** 2026-03-22T21:35:43Z
- **Completed:** 2026-03-22T21:43:38Z
- **Tasks:** 2
- **Files modified:** 9

## Accomplishments
- SerializedPermission record with Owner, OwnerType, OwnerId, Level, LevelValue fields
- PermissionMapper maps DW permissions to DTOs: roles by name, groups by name with numeric OwnerId backup
- Empty Permissions list omitted from YAML output (FileSystemStore already handles OmitEmptyCollections)
- ContentSerializer now queries permissions for every page during serialization
- 17 new tests covering GetLevelName, IsRole, YAML output, round-trip, and DTO construction

## Task Commits

Each task was committed atomically:

1. **Task 1: SerializedPermission DTO + PermissionMapper + unit tests** - `e0a312e` (feat)
2. **Task 2: Wire PermissionMapper into ContentSerializer pipeline** - `78bd2fb` (feat)

## Files Created/Modified
- `src/Dynamicweb.ContentSync/Models/SerializedPermission.cs` - DTO record for a single permission entry
- `src/Dynamicweb.ContentSync/Models/SerializedPage.cs` - Added Permissions list property
- `src/Dynamicweb.ContentSync/Serialization/PermissionMapper.cs` - Reads DW PermissionService, resolves owner names, maps to DTOs
- `src/Dynamicweb.ContentSync/Serialization/ContentMapper.cs` - MapPage accepts and passes through permissions
- `src/Dynamicweb.ContentSync/Serialization/ContentSerializer.cs` - Creates PermissionMapper and calls MapPermissions per page
- `tests/Dynamicweb.ContentSync.Tests/Serialization/PermissionSerializationTests.cs` - GetLevelName, IsRole, YAML output tests
- `tests/Dynamicweb.ContentSync.Tests/Infrastructure/YamlRoundTripTests.cs` - Permission round-trip test
- `tests/Dynamicweb.ContentSync.Tests/Fixtures/ContentTreeBuilder.cs` - BuildSinglePageWithPermissions helper
- `tests/Dynamicweb.ContentSync.Tests/Models/DtoTests.cs` - Permission DTO construction tests

## Decisions Made
- PermissionMapper is a separate class (not inlined in ContentMapper) because it performs I/O via PermissionService and UserManagementServices, keeping ContentMapper as pure conversion
- Used `UserManagementServices.Users.GetUserById` (non-deprecated) instead of `User.GetUserByID`

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed obsolete API usage in PermissionMapper**
- **Found during:** Task 2 (wiring into ContentSerializer)
- **Issue:** `User.GetUserByID(int)` is deprecated with CS0618 warning; recommended replacement is `UserManagementServices.Users.GetUserById`
- **Fix:** Changed to `UserManagementServices.Users.GetUserById(userId)` using `Dynamicweb.Security.UserManagement` namespace
- **Files modified:** src/Dynamicweb.ContentSync/Serialization/PermissionMapper.cs
- **Verification:** Build succeeds with 0 errors, no PermissionMapper warnings
- **Committed in:** 78bd2fb (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug fix)
**Impact on plan:** Necessary fix for deprecated API. No scope creep.

## Issues Encountered
- ForceStringScalarEmitter double-quotes YAML keys, so test assertions needed to check for `"permissions"` substring rather than `permissions:` -- adjusted test to use Contains with bare keyword

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Permission serialization complete, ready for Phase 12 (permission deserialization)
- PermissionMapper.MapPermissions produces the SerializedPermission list that deserialization will consume
- No blockers

## Self-Check: PASSED

- All 4 key files verified on disk
- Both task commits (e0a312e, 78bd2fb) verified in git log

---
*Phase: 11-permission-serialization*
*Completed: 2026-03-22*
