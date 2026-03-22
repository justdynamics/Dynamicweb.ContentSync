# Phase 10: Context Menu Actions - Context

**Gathered:** 2026-03-22
**Status:** Ready for planning

<domain>
## Phase Boundary

Add Serialize and Deserialize context menu actions to every page node in the DW content tree. Serialize produces a zip of the page subtree as YAML files (plus a log file) and triggers a browser download + saves to a configurable disk location. Deserialize uploads a zip, validates it, then applies content with one of three modes: overwrite, add as children, or add as sibling. Reuses existing ContentSerializer/ContentDeserializer — no duplication of serialization logic.

</domain>

<decisions>
## Implementation Decisions

### Serialize Behavior
- **D-01:** Zip contains YAML files in mirror-tree layout plus a log file — no config or metadata manifests
- **D-02:** Serialize runs synchronously — user clicks, waits, gets download. No async/progress bar.
- **D-03:** Download filename format: `ContentSync_{PageName}_{date}.zip` (e.g. `ContentSync_CustomerCenter_2026-03-22.zip`)
- **D-04:** Zip is also saved to a configurable export path on disk — new config field `ExportDirectory` in SyncConfiguration
- **D-05:** Reuse ContentSerializer with a temporary SyncConfiguration scoped to the clicked page's subtree

### Deserialize UX Flow
- **D-06:** Single modal dialog with: file upload field, mode selection (radio/dropdown), and Go button
- **D-07:** Three import modes available as user choice:
  - **Overwrite**: Replace the clicked page AND its subtree with the zip root page and subtree. Show a warning tip: "This will replace the selected page and all its children."
  - **Add as children**: Zip content becomes children under the clicked page
  - **Add as sibling**: Zip root page appears next to the clicked page at the same level
- **D-08:** Validate-then-apply strategy: parse and validate the entire zip before touching the database. If validation passes, apply page by page. If a runtime error occurs during apply, continue with remaining pages and report failures.
- **D-09:** Show a summary after deserialize: "X/Y pages imported successfully. Z failed: [reasons]"
- **D-10:** Reuse ContentDeserializer with a temporary SyncConfiguration scoped to the target location

### Context Menu Injection
- **D-11:** Serialize and Deserialize actions appear on every page node in the content tree — no predicate filtering
- **D-12:** Actions appear in their own "Content Sync" action group at the bottom of the right-click context menu
- **D-13:** Use DW's ScreenInjector or equivalent pattern to inject actions into the content tree page list screen

### Claude's Discretion
- How to inject context menu actions into DW's content tree (ScreenInjector target type, ListScreenInjector vs other pattern — needs research in DW10 source)
- How to implement file upload in the deserialize modal (DW's file upload mechanism is undocumented — research needed)
- How to trigger browser download after serialize (streaming response vs file action)
- Temporary directory management for serialize (System temp, cleanup strategy)
- How to scope ContentSerializer/ContentDeserializer to a single page subtree via temp SyncConfiguration
- ExportDirectory config field placement and default value

</decisions>

<specifics>
## Specific Ideas

- The overwrite mode warning tip is important for UX safety — make it prominent in the modal
- Export path should be a new field on the settings screen (Phase 8 extension) or just a config-only field
- Log file in the zip should capture what was serialized (page count, area, timestamp)
- ContentSerializer already accepts a SyncConfiguration — create a temp one with a single predicate targeting the clicked page

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Existing ContentSync Core (reuse these)
- `src/Dynamicweb.ContentSync/Serialization/ContentSerializer.cs` — Serialize content tree to YAML files on disk
- `src/Dynamicweb.ContentSync/Serialization/ContentDeserializer.cs` — Deserialize YAML files into DW database
- `src/Dynamicweb.ContentSync/Configuration/SyncConfiguration.cs` — Config record with Predicates list (create temp config with single predicate)
- `src/Dynamicweb.ContentSync/Configuration/PredicateDefinition.cs` — Predicate targeting a page path + area
- `src/Dynamicweb.ContentSync/Configuration/ConfigWriter.cs` — For persisting new ExportDirectory field
- `src/Dynamicweb.ContentSync/Configuration/ConfigLoader.cs` — For reading ExportDirectory

### Existing Admin UI Infrastructure (extend these)
- `src/Dynamicweb.ContentSync/AdminUI/Tree/SyncSettingsNodeProvider.cs` — Tree node registration pattern
- `src/Dynamicweb.ContentSync/AdminUI/Screens/SyncSettingsEditScreen.cs` — Settings screen (may need ExportDirectory field)
- `src/Dynamicweb.ContentSync/AdminUI/Models/SyncSettingsModel.cs` — Settings model
- `src/Dynamicweb.ContentSync/AdminUI/Commands/SaveSyncSettingsCommand.cs` — Save command

### DW Source & Patterns (for context menu research)
- `C:\Projects\temp\dw10source\` — Search for ListScreenInjector, content tree page list screen, context menu action injection, file upload/download patterns
- `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Screens\ListScreenInjector.cs` — Base class for injecting actions into list screens
- `C:\Projects\temp\dwextensionsample\Samples-main\ExpressDelivery\ExpressDelivery\Injectors\OrderOverviewInjector.cs` — Injector example from ExpressDelivery
- `C:\VibeCode\DynamicWeb.AIDiagnoser\` — Prior project with DW admin UI learnings

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ContentSerializer.Serialize(SyncConfiguration)`: Accepts config and serializes matching content to disk. Create temp config with single predicate for ad-hoc serialize.
- `ContentDeserializer.Deserialize(SyncConfiguration)`: Accepts config and deserializes from disk to DB. Create temp config pointing to extracted zip location.
- Phase 7-9 CQRS patterns (CommandBase, queries, screens): Reuse for any new commands/screens needed for the modal flow.

### Established Patterns
- `ListScreenInjector<TScreen, TRowModel>` for injecting actions into existing screens
- `ActionBuilder.Delete()` pattern for confirmation dialogs — adapt for deserialize warning
- `CommandBase.Handle()` returning `CommandResult` for operation results
- `SelectorBuilder` for dropdowns (import mode selection)

### Integration Points
- Content tree page list screen (need to identify the DW class name — research required)
- File upload mechanism in DW admin UI (undocumented — research required)
- Browser download triggering from a command result (research required)
- SyncConfiguration extension with ExportDirectory field

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 10-context-menu-actions*
*Context gathered: 2026-03-22*
