---
phase: 21-paragraph-anchor-resolution
plan: 01
subsystem: serialization
tags: [regex, link-resolution, paragraph-anchors, tdd]

requires:
  - phase: 20-link-resolution-core
    provides: "InternalLinkResolver with page ID rewriting and ResolveLinksInArea wiring"
  - phase: 19-source-id-serialization
    provides: "SourceParagraphId in SerializedParagraph YAML"
provides:
  - "Paragraph anchor resolution in Default.aspx?ID=NNN#PPP links"
  - "BuildSourceToTargetParagraphMap static method for paragraph ID mapping"
  - "Paragraph GUID cache construction in ContentDeserializer Phase 2"
affects: []

tech-stack:
  added: []
  patterns: ["Recursive tree walk for paragraph map mirroring page map pattern"]

key-files:
  created: []
  modified:
    - src/DynamicWeb.Serializer/Serialization/InternalLinkResolver.cs
    - src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs
    - tests/DynamicWeb.Serializer.Tests/Serialization/InternalLinkResolverTests.cs

key-decisions:
  - "Extended regex with optional fragment group rather than separate pass"
  - "4-tuple GetStats return (page resolved/unresolved + paragraph resolved/unresolved)"

patterns-established:
  - "Paragraph GUID cache built same way as page GUID cache (Services.Paragraphs.GetParagraphsByPageId)"

requirements-completed: [LINK-05]

duration: 3min
completed: 2026-04-03
---

# Phase 21 Plan 01: Paragraph Anchor Resolution Summary

**Extended InternalLinkResolver to resolve Default.aspx?ID=NNN#PPP anchor fragments with both page and paragraph ID rewriting, wired via paragraph GUID cache in ContentDeserializer**

## Performance

- **Duration:** 3 min
- **Started:** 2026-04-03T13:42:25Z
- **Completed:** 2026-04-03T13:45:52Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- Paragraph anchor fragments (#PPP) in Default.aspx links now have both page ID and paragraph ID resolved to target environment values
- BuildSourceToTargetParagraphMap recursively walks page tree (GridRows -> Columns -> Paragraphs) to build source-to-target paragraph ID map
- ContentDeserializer Phase 2 builds paragraph GUID cache and passes paragraph map to resolver
- 7 new tests added (23 total), all passing with zero regressions

## Task Commits

Each task was committed atomically:

1. **Task 1 RED: TDD failing tests** - `a4c0249` (test)
2. **Task 1 GREEN: Implement paragraph anchor resolution** - `274dced` (feat)
3. **Task 2: Wire paragraph map into ContentDeserializer** - `555696a` (feat)

## Files Created/Modified
- `src/DynamicWeb.Serializer/Serialization/InternalLinkResolver.cs` - Extended regex, constructor, ResolveLinks evaluator, BuildSourceToTargetParagraphMap, 4-tuple GetStats
- `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` - Paragraph GUID cache construction, paragraph map wiring, updated stats logging
- `tests/DynamicWeb.Serializer.Tests/Serialization/InternalLinkResolverTests.cs` - 7 new tests for paragraph anchor resolution and paragraph map building

## Decisions Made
- Extended existing regex with optional `(#(\d+))?` group rather than a separate regex pass -- simpler, single match handles both cases
- GetStats returns 4-tuple `(resolved, unresolved, paragraphResolved, paragraphUnresolved)` to keep a single stats call
- Paragraph GUID cache built in Phase 2 block (separate from ResolveLinksInArea) since it's for resolver constructor, not per-page iteration

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- LINK-05 requirement satisfied
- Phase 21 complete (1/1 plans)
- Phase 22 (Version Housekeeping) is independent and can proceed anytime

---
*Phase: 21-paragraph-anchor-resolution*
*Completed: 2026-04-03*
