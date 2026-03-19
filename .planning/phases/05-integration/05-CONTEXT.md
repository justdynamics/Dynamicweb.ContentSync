# Phase 5: Integration - Context

**Gathered:** 2026-03-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Wire ContentSync as a distributable DynamicWeb AppStore app. Both scheduled tasks (serialize and deserialize) already exist — this phase adds NuGet package metadata, enhances serializer logging to meet OPS-03, adds end-to-end integration tests that verify the scheduled task entry points produce correct output, and ensures the package is distributable as a NuGet package reference.

</domain>

<decisions>
## Implementation Decisions

### NuGet Package Shape (INF-01)
- PackageId: `Dynamicweb.ContentSync` — matches assembly name, consistent with DW AppStore conventions
- Version: `0.1.0-beta` — pre-release tag, first beta before committing to stable API
- Tags: must include `dynamicweb-app-store` and `task` per DW AppStore requirements
- Switch from DLL references to NuGet package references for Dynamicweb.dll and Dynamicweb.Core.dll — cleaner dependency resolution for distributable package
- Standard NuGet metadata to add: Authors, Description, License, ProjectUrl, RepositoryUrl

### End-to-End Verification
- Integration tests only — no manual DW admin testing required
- Extend existing integration test project to instantiate and call both scheduled tasks programmatically
- Verifies the full pipeline without needing the DW admin UI
- Byte-identical YAML output: serialize task via scheduled task entry point must produce identical output to calling ContentSerializer directly. Deterministic serialization guarantees from Phase 1 apply
- Compare directory trees to assert identical output

### Serialize Logging Parity (OPS-03)
- No SerializeResult object — current basic logging is sufficient for the serialize side
- Add a count summary line at end of serialization: "Serialization complete: X pages, Y grid rows, Z paragraphs serialized"
- Lightweight enhancement — keep existing `Action<string>` logging pattern, just add aggregate counts
- OPS-03's full per-item structured summary (new/updated/skipped/failed with error details) applies primarily to deserialization, which already has DeserializeResult

### Claude's Discretion
- Exact NuGet package metadata values (Authors, Description text, license type)
- Which Dynamicweb NuGet packages to reference and their versions (research needed — known issue from Phase 3 that NuGet packages may not include all namespaces)
- How to structure the count tracking in ContentSerializer (local variables vs a lightweight struct)
- Integration test assertions and helper methods
- Whether to add a sample ContentSync.config.json to the NuGet package content files

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Existing Scheduled Tasks (already implemented)
- `src/Dynamicweb.ContentSync/ScheduledTasks/SerializeScheduledTask.cs` — Serialize task with config discovery, logging, ContentSerializer invocation
- `src/Dynamicweb.ContentSync/ScheduledTasks/DeserializeScheduledTask.cs` — Deserialize task mirroring serialize, with DeserializeResult handling

### Serialization/Deserialization Pipeline
- `src/Dynamicweb.ContentSync/Serialization/ContentSerializer.cs` — Serialize orchestrator (needs count summary enhancement)
- `src/Dynamicweb.ContentSync/Serialization/ContentDeserializer.cs` — Deserialize orchestrator (already has full structured logging)
- `src/Dynamicweb.ContentSync/Serialization/DeserializeResult.cs` — Result record pattern to reference (not to replicate for serialize)

### Project Configuration
- `src/Dynamicweb.ContentSync/Dynamicweb.ContentSync.csproj` — Main project file, needs NuGet metadata added
- `src/Dynamicweb.ContentSync/Configuration/ConfigLoader.cs` — Config loading (used by both tasks)
- `src/Dynamicweb.ContentSync/Configuration/SyncConfiguration.cs` — Configuration record

### Existing Tests
- `tests/Dynamicweb.ContentSync.IntegrationTests/Serialization/CustomerCenterSerializationTests.cs` — Serialize integration tests (pattern to follow)
- `tests/Dynamicweb.ContentSync.IntegrationTests/Deserialization/CustomerCenterDeserializationTests.cs` — Deserialize integration tests

### Project Requirements
- `.planning/REQUIREMENTS.md` — OPS-01 (serialize task), OPS-02 (deserialize task), OPS-03 (structured logging), INF-01 (AppStore app)
- `.planning/PROJECT.md` — DW AppStore app structure, NuGet tags, scheduled task attributes

### DynamicWeb AppStore
- DynamicWeb AppStore guide: https://doc.dynamicweb.dev/documentation/extending/guides/newappstoreapp.html
- DynamicWeb scheduled tasks: https://doc.dynamicweb.dev/documentation/extending/extensibilitypoints/scheduled-task-addins.html

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SerializeScheduledTask` — Already complete with `[AddInName("ContentSync.Serialize")]`, config discovery, logging. Just needs its output verified by integration tests
- `DeserializeScheduledTask` — Already complete with `[AddInName("ContentSync.Deserialize")]`, mirrors serialize task. Calls `ContentDeserializer.Deserialize()` and logs `result.Summary`
- `ContentSerializer` — Fully functional, needs only a count summary line added at end
- `ContentDeserializer` — Fully functional with DeserializeResult and structured logging
- `FileSystemStore.WriteTree()`/`ReadTree()` — Deterministic I/O for byte-identical comparison in tests
- Existing integration test patterns in both Serialization/ and Deserialization/ test directories

### Established Patterns
- `Action<string>` log delegate passed via constructor (not ILogger)
- `BaseScheduledTaskAddIn` inheritance with `[AddInName]`, `[AddInLabel]`, `[AddInDescription]` attributes
- Config discovery: 4-path search cascade for `ContentSync.config.json`
- `Log()` method with file-based output: `[yyyy-MM-dd HH:mm:ss.fff] message` format
- xunit integration tests with `[Fact]` and direct DW service calls

### Integration Points
- Scheduled tasks are the public entry points — DW admin discovers them via attributes
- Both tasks share ConfigLoader, logging, and their respective pipeline classes
- NuGet package is the distribution mechanism — consumers add a package reference and tasks appear in DW admin
- Integration tests verify the full stack: scheduled task → config → pipeline → file I/O / DW writes

</code_context>

<specifics>
## Specific Ideas

- Both scheduled tasks are already implemented and tested individually — Phase 5 focuses on packaging, logging polish, and end-to-end verification through the task entry points
- The NuGet package switching from DLL references to NuGet package references may need research to find the right Dynamicweb NuGet packages (Phase 3 discovered that `Dynamicweb.Core` NuGet lacks `Dynamicweb.Content` namespace)
- Byte-identical comparison for serialize E2E: run ContentSerializer directly, then run SerializeScheduledTask, diff the output directories

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 05-integration*
*Context gathered: 2026-03-19*
