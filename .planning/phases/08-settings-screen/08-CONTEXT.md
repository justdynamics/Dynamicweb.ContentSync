# Phase 8: Settings Screen - Context

**Gathered:** 2026-03-22
**Status:** Ready for planning

<domain>
## Phase Boundary

Complete the settings edit screen at Settings > Content > Sync with all config fields (OutputDirectory, LogLevel, DryRun, ConflictStrategy). Phase 7 built the skeleton screen, CQRS infrastructure, and 2 of 4 fields. This phase adds the remaining fields, validation, and config model expansion. Predicate management is Phase 9 scope.

</domain>

<decisions>
## Implementation Decisions

### Field Controls
- **D-01:** LogLevel renders as a dropdown with fixed options: Info, Debug, Warn, Error (prevents typos, replaces current free-text input)
- **D-02:** ConflictStrategy renders as a dropdown showing "Source Wins" for now, ready for future options
- **D-03:** DryRun renders as a checkbox (on/off toggle)
- **D-04:** All fields in one flat section "Content Sync" (matches existing skeleton, no sub-grouping)

### Validation
- **D-05:** OutputDirectory is validated on save — path must exist on disk relative to the host's `wwwroot/Files/System` directory. Reject save if directory doesn't exist.
- **D-06:** OutputDirectory defaults to `\System\ContentSync` (relative to Files/System within the DW host)
- **D-07:** OutputDirectory is required — block save with error message if empty
- **D-08:** Validation uses DW's built-in screen validation (inline field errors, same pattern as other DW settings screens)
- **D-09:** LogLevel needs no extra validation beyond dropdown constraint — dropdown already constrains to valid values

### Config Model Expansion
- **D-10:** Existing config files without `dryRun` or `conflictStrategy` keys silently get defaults applied (DryRun=false, ConflictStrategy="source-wins") — no file migration needed
- **D-11:** camelCase JSON property naming for new fields (`dryRun`, `conflictStrategy`) — matches existing ConfigLoader convention and JSON industry standard
- **D-12:** ConflictStrategy stored as simple string in JSON: `"conflictStrategy": "source-wins"` — simple and extensible
- **D-13:** ConflictStrategy represented as C# enum (`ConflictStrategy.SourceWins`) — type-safe, dropdown auto-populates from enum values

### Claude's Discretion
- Exact dropdown implementation pattern (ConfigurableProperty attributes vs custom editor)
- How to resolve OutputDirectory relative path against Files/System at validation time
- Enum serialization/deserialization configuration in System.Text.Json
- Error message wording for validation failures

</decisions>

<specifics>
## Specific Ideas

- OutputDirectory should be relative to the host's `wwwroot/Files/System` directory, defaulting to `\System\ContentSync`
- Check ExpressDelivery sample and DW10 source for dropdown/enum field patterns in EditScreenBase
- Phase 7's SyncSettingsModel already has OutputDirectory and LogLevel — extend it, don't replace

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Existing Phase 7 Infrastructure (extend these)
- `src/Dynamicweb.ContentSync/AdminUI/Models/SyncSettingsModel.cs` — DataViewModelBase with ConfigurableProperty fields (add DryRun, ConflictStrategy)
- `src/Dynamicweb.ContentSync/AdminUI/Screens/SyncSettingsEditScreen.cs` — EditScreenBase skeleton (add EditorFor for new fields)
- `src/Dynamicweb.ContentSync/AdminUI/Queries/SyncSettingsQuery.cs` — Query that loads config fresh from disk (map new fields)
- `src/Dynamicweb.ContentSync/AdminUI/Commands/SaveSyncSettingsCommand.cs` — Save command (persist new fields, add validation)
- `src/Dynamicweb.ContentSync/Configuration/SyncConfiguration.cs` — Config record (add DryRun bool, ConflictStrategy enum)
- `src/Dynamicweb.ContentSync/Configuration/ConfigLoader.cs` — JSON deserialization (handle new fields with defaults)
- `src/Dynamicweb.ContentSync/Configuration/ConfigWriter.cs` — JSON serialization (write new fields)

### DW Extension Patterns
- `C:\Projects\temp\dwextensionsample\Samples-main\ExpressDelivery\ExpressDelivery\Screens\ExpressDeliveryPresetEditScreen.cs` — EditScreenBase pattern with field editors
- `C:\Projects\temp\dw10source\` — Search for dropdown/enum ConfigurableProperty usage, validation patterns in EditScreenBase

### DW Admin UI Pitfalls
- `C:\VibeCode\DynamicWeb.AIDiagnoser\` — Prior project with DW admin UI integration learnings

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SyncSettingsModel`: Already has OutputDirectory and LogLevel — add DryRun (bool) and ConflictStrategy (enum) properties
- `SyncSettingsEditScreen.BuildEditScreen()`: Already has EditorFor pattern — add editors for new fields
- `SyncSettingsQuery.GetModel()`: Already maps config to model — extend mapping
- `SaveSyncSettingsCommand.Handle()`: Already persists via ConfigWriter — extend with new fields and validation
- `ConfigPathResolver.FindOrCreateConfigFile()`: Handles config file creation with defaults

### Established Patterns
- `ConfigurableProperty` attribute for field labels and explanations
- `EditorFor(m => m.Property)` for adding fields to screen
- `CommandResult.ResultType.Invalid` for validation errors
- `ConfigLoader.Load()` + `ConfigWriter.Save()` round-trip pattern
- camelCase JSON serialization (System.Text.Json)

### Integration Points
- SyncConfiguration record needs DryRun and ConflictStrategy added — affects ConfigLoader deserialization and ConfigWriter serialization
- Scheduled tasks (SerializeScheduledTask, DeserializeScheduledTask) already read SyncConfiguration — they'll pick up new fields automatically
- OutputDirectory validation needs access to DW host's physical Files/System path at save time

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 08-settings-screen*
*Context gathered: 2026-03-22*
