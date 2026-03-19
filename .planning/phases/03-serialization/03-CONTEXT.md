# Phase 3: Serialization - Context

**Gathered:** 2026-03-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Build the DynamicWeb-to-disk serialization pipeline. Connect to live DW APIs (AreaService, PageService, GridService, ParagraphService), traverse content trees filtered by predicates, map DW objects to DTOs, and write YAML files via FileSystemStore. Verify with the Customer Center tree (pageid=8385) on the Swift2.2 test instance. Source-wins is established by the serializer producing files that ARE the source of truth.

</domain>

<decisions>
## Implementation Decisions

### DW API Traversal
- Entry point: Area → Pages → recursive walk of children
- Load area by predicate's `areaId`, find root page matching predicate path, recursively walk child pages
- For each page: load grid rows via GridService, paragraphs via ParagraphService
- Apply ContentPredicateSet filtering at each page node to check include/exclude rules
- Mapping strategy: Claude's discretion — curated core properties or reflection-based, whatever produces best fidelity

### Reference Field Handling
- GUID-map known reference fields at serialize time
- For known references (ParentPageId, page links, etc.), replace numeric ID with the target item's GUID
- Requires a lookup per reference to resolve numeric ID → GUID
- Unknown/undocumented reference fields: discover empirically against the test instance (known blocker from research)

### Test Environment
- **Swift2.2** (.NET 10) is the SOURCE — serialize from this instance
- **Swift2.1** (.NET 8) is the TARGET — deserialize into this instance (Phase 4)
- Both instances at `C:\Projects\Solutions\swift.test.forsync`
- Start with `dotnet run` in the respective folders
- Test case: Customer Center content tree, pageid=8385

### Test Strategy
- Live instance integration tests — connect to running Swift2.2, serialize Customer Center
- Verify both structure (folder tree, file count) AND content (spot-check field values in YAML)
- No mocks for DW services — test against the real API for confidence

### Deployment to Test Instance
- DLL copy approach — build ContentSync, copy DLL + dependencies to swift instance's bin folder
- Quick and dirty, appropriate for development/testing phase
- Target **net8 only** — net8 assemblies load fine in net10, keeps it simple

### Source-wins Establishment
- Source-wins conflict strategy is established by the serializer: files on disk ARE the source of truth
- The serializer writes files that the deserializer (Phase 4) will treat as authoritative
- No merge logic — files always overwrite target DB on deserialize

### YAML Fidelity (INF-02)
- Already proven in Phase 1 with ForceStringScalarEmitter
- Phase 3 validates against real DW content (HTML fields, multiline descriptions, etc.)
- Any new edge cases from live content must be handled without breaking existing fidelity tests

### Windows Long-Path (INF-03)
- Serializer must handle paths exceeding Windows MAX_PATH (260 chars)
- Deep content hierarchies with long page names can exceed this limit
- Warn and skip items that would overflow, do not crash

### Claude's Discretion
- Exact DW API method signatures and service resolution
- How to resolve DW services (dependency injection, static access, or direct construction)
- Which properties count as "known reference fields" — discover during research/implementation
- Integration test project structure and test runner configuration

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### DynamicWeb APIs
- `.planning/research/ARCHITECTURE.md` — Component boundaries, DW service API mapping, content hierarchy traversal
- `.planning/research/STACK.md` — Dynamicweb.Core 10.23.9 APIs, service namespaces
- DynamicWeb Content API: https://doc.dynamicweb.dev/api/Dynamicweb.Content.html
- DynamicWeb Content manual: https://doc.dynamicweb.dev/manual/dynamicweb10/content/index.html

### Existing Codebase
- `src/Dynamicweb.ContentSync/Models/` — DTO records (SerializedArea, SerializedPage, etc.)
- `src/Dynamicweb.ContentSync/Infrastructure/FileSystemStore.cs` — mirror-tree I/O
- `src/Dynamicweb.ContentSync/Infrastructure/YamlConfiguration.cs` — YAML serializer config
- `src/Dynamicweb.ContentSync/Configuration/ConfigLoader.cs` — config loading
- `src/Dynamicweb.ContentSync/Configuration/ContentPredicate.cs` — predicate evaluation

### Project Requirements
- `.planning/REQUIREMENTS.md` — SER-03 (source-wins), INF-02 (YAML fidelity), INF-03 (long-path)
- `.planning/PROJECT.md` — Core value, constraints, ID strategy

### Known Blockers
- `.planning/research/PITFALLS.md` — Reference field inventory, numeric ID leakage, non-deterministic sort order

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `FileSystemStore.WriteTree(SerializedArea)` — writes full content tree to disk as mirror-tree YAML
- `FileSystemStore.ReadTree(string)` — reads YAML files back to DTOs
- `YamlConfiguration.BuildSerializer()` / `BuildDeserializer()` — configured YAML engine with fidelity guarantees
- `ConfigLoader.Load(string)` — loads and validates ContentSync.config.json
- `ContentPredicateSet` — evaluates include/exclude rules against content paths
- `ContentTreeBuilder` — test fixture for building sample content trees

### Established Patterns
- Record types for DTOs with `Dictionary<string, object>` Fields
- Children collections for hierarchy (Page → GridRows, GridRow → Paragraphs)
- ForceStringScalarEmitter for YAML fidelity
- xunit with Theory/InlineData for parameterized tests

### Integration Points
- New: DW API services → DTO mapper (the core of this phase)
- FileSystemStore consumes DTOs from the mapper
- ContentPredicateSet filters which pages to include
- ConfigLoader provides output directory and predicate definitions

</code_context>

<specifics>
## Specific Ideas

- Test the serializer against Customer Center (pageid=8385) on Swift2.2 — verify both structure and content
- Reference field discovery is empirical — inspect actual DW Page/Paragraph objects at runtime to identify which fields carry numeric cross-item references
- The serializer is the component that "establishes" source-wins by producing the authoritative YAML files

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 03-serialization*
*Context gathered: 2026-03-19*
