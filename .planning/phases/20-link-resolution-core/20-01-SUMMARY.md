---
phase: 20-link-resolution-core
plan: 01
subsystem: serialization
tags: [regex, link-resolution, tdd, internal-links]

# Dependency graph
requires:
  - phase: 19-source-id-serialization
    provides: SourcePageId field on SerializedPage DTO
provides:
  - InternalLinkResolver class with ResolveLinks, BuildSourceToTargetMap, GetStats
  - Boundary-aware regex pattern for Default.aspx?ID=NNN rewriting
  - 16 unit tests covering all link resolution edge cases
affects: [20-link-resolution-core plan 02, 21-paragraph-anchor-resolution]

# Tech tracking
tech-stack:
  added: []
  patterns: [stateless-helper-with-constructor-injection, boundary-aware-regex-replace]

key-files:
  created:
    - src/DynamicWeb.Serializer/Serialization/InternalLinkResolver.cs
    - tests/DynamicWeb.Serializer.Tests/Serialization/InternalLinkResolverTests.cs
  modified: []

key-decisions:
  - "Regex greedy \\d+ provides natural boundary safety without explicit lookahead"
  - "Case-insensitive regex handles default.aspx?id= variants"
  - "Recursive tree flattening in BuildSourceToTargetMap handles nested page children"

patterns-established:
  - "InternalLinkResolver: stateless helper with Dictionary<int,int> map + Action<string> logger injection"
  - "BuildSourceToTargetMap: static factory combining SourcePageId + PageGuidCache into mapping dictionary"

requirements-completed: [LINK-01, LINK-02, LINK-04]

# Metrics
duration: 5min
completed: 2026-04-03
---

# Phase 20 Plan 01: InternalLinkResolver Summary

**Boundary-aware regex resolver rewrites Default.aspx?ID=NNN patterns using source-to-target page ID map with TDD coverage**

## Performance

- **Duration:** 5 min
- **Started:** 2026-04-03T12:47:14Z
- **Completed:** 2026-04-03T12:52:44Z
- **Tasks:** 1 (TDD: RED + GREEN)
- **Files modified:** 2 created, 4 fixed (pre-existing)

## Accomplishments
- InternalLinkResolver with ResolveLinks method using compiled, case-insensitive regex
- BuildSourceToTargetMap static method combining SourcePageId + PageGuidCache with recursive tree flattening
- GetStats method tracking resolved/unresolved counts across calls
- 16 unit tests covering: simple rewrite, multiple links, HTML, boundary safety, unresolvable, no links, case insensitivity, query params, null/empty, anchor preservation, map building, nested children, stats

## Task Commits

Each task was committed atomically:

1. **Task 1 RED: Failing tests** - `6f86f28` (test)
2. **Task 1 GREEN: Implementation** - `29d4d11` (feat)
3. **Pre-existing SqlTable test fixes** - `27792d1` (fix - deviation)

## Files Created/Modified
- `src/DynamicWeb.Serializer/Serialization/InternalLinkResolver.cs` - Stateless link resolver with ResolveLinks, BuildSourceToTargetMap, GetStats
- `tests/DynamicWeb.Serializer.Tests/Serialization/InternalLinkResolverTests.cs` - 16 unit tests covering all edge cases

## Decisions Made
- Regex greedy `\d+` provides natural boundary safety -- no explicit lookahead needed because the regex engine captures the full integer
- Case-insensitive matching via `RegexOptions.IgnoreCase` handles all DW URL case variants
- Warning callback pattern matches PermissionMapper for consistency

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed pre-existing SqlTable test compilation errors**
- **Found during:** Task 1 RED phase (test execution)
- **Issue:** 31 compilation errors in SqlTable test files (string[] vs List<string>, Moq expression tree optional args) prevented the entire test project from building
- **Fix:** Converted `new[]` initializers to `new List<string>` for TableMetadata properties; added explicit optional parameters to Moq `Setup`/`Verify` expression trees
- **Files modified:** SqlTableWriterTests.cs, SqlTableProviderDeserializeTests.cs, IdentityResolutionTests.cs, FlatFileStoreTests.cs
- **Verification:** `dotnet build tests/DynamicWeb.Serializer.Tests` succeeds with 0 errors
- **Committed in:** `27792d1`

---

**Total deviations:** 1 auto-fixed (Rule 3 blocking)
**Impact on plan:** Pre-existing errors blocked all test execution. Fix was minimal and mechanical. No scope creep.

## Issues Encountered
None beyond the pre-existing compilation errors documented above.

## Known Stubs
None - all methods fully implemented and tested.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- InternalLinkResolver ready to be wired into ContentDeserializer in plan 20-02
- BuildSourceToTargetMap provides the dictionary construction that 20-02 will call after Phase 1 deserialization
- GetStats enables logging resolved/unresolved counts in the deserialization output

---
*Phase: 20-link-resolution-core*
*Completed: 2026-04-03*
