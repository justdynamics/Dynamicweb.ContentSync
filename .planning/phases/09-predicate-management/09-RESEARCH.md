# Phase 9: Predicate Management - Research

**Researched:** 2026-03-22
**Domain:** DW10 Admin UI CRUD (ListScreen + EditScreen + CQRS commands for JSON-backed predicates)
**Confidence:** HIGH

## Summary

Phase 9 adds full CRUD management for content sync predicates in the DW admin UI. The existing `PredicateDefinition` model, `ConfigLoader`/`ConfigWriter` infrastructure, and `SyncSettingsNodeProvider` (with a shell Predicates sub-node) provide the foundation. The implementation follows the proven `ListScreenBase<T>` / `EditScreenBase<T>` / `CommandBase<T>` pattern from ExpressDelivery samples and Phase 8's settings screen.

The two hardest problems are solved by DW's built-in `SelectorBuilder` API: `SelectorBuilder.CreatePageSelector()` provides the content tree picker for the Path field, and `SelectorBuilder.CreateAreaSelector()` provides the area dropdown. Both are well-documented in DW10 source with dozens of usage examples across the codebase. The page selector returns an **int page ID**, but `PredicateDefinition.Path` stores a **string path** -- the edit screen must convert between them using `Services.Pages.GetPage(id).GetBreadcrumbPath()`.

**Primary recommendation:** Follow ExpressDelivery preset CRUD pattern exactly, using index-based identity (array position) instead of DB IDs. Use `SelectorBuilder.CreatePageSelector()` and `SelectorBuilder.CreateAreaSelector()` for the picker fields. Fix ConfigLoader to allow zero predicates (D-15 conflict).

<user_constraints>

## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** List screen uses `ListScreenBase<T>` with `RowViewMapping` -- same pattern as ExpressDelivery PresetListScreen
- **D-02:** Columns: Name, Path, Area Name (resolved from AreaId via `Services.Areas`)
- **D-03:** No sorting or filtering -- flat list, most installs will have 1-5 predicates
- **D-04:** Empty state uses DW's default empty list rendering (no custom message) -- Add button always visible in toolbar
- **D-05:** Row click navigates to edit screen. Right-click context menu shows Edit + Delete (ActionBuilder pattern from ExpressDelivery)
- **D-06:** Add button in toolbar creates new predicate (navigates to blank edit screen)
- **D-07:** Edit screen uses `EditScreenBase<T>` with fields: Name (text, required), Path (content tree picker, required), Area (dropdown of DW areas, required), Excludes (multi-line textarea, optional)
- **D-08:** Area field is a Select dropdown populated from `Dynamicweb.Content.Services.Areas` -- shows area names, stores numeric AreaId
- **D-09:** Path field uses DW's content tree picker (page selection UI) -- NOT free-text. This is a hard requirement for usability.
- **D-10:** Excludes entered as multi-line text (one path per line). Split on newlines when saving to the `Excludes` string list.
- **D-11:** Validation: Name required, Path required, Area required. Unique name enforced (reject duplicate predicate names). Path existence validated against the selected area's content tree.
- **D-12:** Save writes immediately to ContentSync.config.json via ConfigWriter (same pattern as Phase 8 SaveSyncSettingsCommand)
- **D-13:** Standard DW confirmation dialog via `ActionBuilder.Delete` -- "Are you sure you want to delete predicate '{Name}'?"
- **D-14:** Delete is immediate -- writes to config file on confirm (no batched save)
- **D-15:** Deleting the last predicate is allowed -- zero predicates = nothing syncs, valid state
- **D-16:** Predicates are identified by array index in the JSON config (no DB-assigned IDs). Edit/delete commands use the index to locate the target predicate.

### Claude's Discretion
- How to implement the content tree picker (researched -- use `SelectorBuilder.CreatePageSelector()`)
- How to resolve area name from AreaId for the list column display (researched -- use `Services.Areas.GetArea(id)?.Name`)
- DataViewModelBase vs DataQueryModelBase for the list model (researched -- use `DataQueryModelBase<DataListViewModel<T>>` for list query)
- Navigation wiring between list screen and edit screen (query parameter passing for index-based identity)
- How to handle the "new predicate" case (index = -1 or similar sentinel)

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope

</user_constraints>

<phase_requirements>

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PRED-01 | Predicates sub-node appears under Sync node in admin navigation | SyncSettingsNodeProvider already has shell node at line 39-47; just add `NodeAction = NavigateScreenAction.To<PredicateListScreen>()` |
| PRED-02 | User can view list of configured predicates (name, path, include/exclude) | ListScreenBase + RowViewMapping pattern from ExpressDelivery; DataListViewModel<PredicateListModel> |
| PRED-03 | User can add a new predicate with name, path, include/exclude | EditScreenBase + SelectorBuilder.CreatePageSelector + SelectorBuilder.CreateAreaSelector; new predicate = index -1 |
| PRED-04 | User can edit an existing predicate | Same edit screen, loaded via index-based query; page ID stored on model for selector pre-selection |
| PRED-05 | User can delete a predicate | CommandBase with index-based delete; ActionBuilder.Delete for confirmation |
| PRED-06 | Predicate changes persist to ContentSync.config.json | ConfigWriter.Save pattern from Phase 8; load-modify-save cycle |

</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Dynamicweb.CoreUI | 10.x (bundled) | ListScreenBase, EditScreenBase, CommandBase, DataViewModelBase | DW admin UI framework |
| Dynamicweb.Content.UI | 10.23.9 | SelectorBuilder (page/area selectors), Services.Areas, Services.Pages | Already referenced in csproj from Phase 7 |
| System.Text.Json | 8.x (bundled) | JSON serialization for config file | Already used by ConfigWriter/ConfigLoader |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Dynamicweb.CoreUI.Editors.Selectors | (part of CoreUI) | SelectorBuilder, Selector, SelectorProviderBase | Page picker and area dropdown editors |
| Dynamicweb.CoreUI.Editors.Inputs | (part of CoreUI) | Text, Textarea, Select editors | Form field editors |

No new NuGet packages needed. All dependencies are already present from Phase 7/8.

## Architecture Patterns

### Recommended Project Structure
```
src/Dynamicweb.ContentSync/AdminUI/
├── Commands/
│   ├── SaveSyncSettingsCommand.cs          # (existing, Phase 8)
│   ├── SavePredicateCommand.cs             # NEW: save/create predicate
│   └── DeletePredicateCommand.cs           # NEW: delete predicate by index
├── Models/
│   ├── SyncSettingsModel.cs                # (existing, Phase 8)
│   ├── PredicateListModel.cs               # NEW: row model for list screen
│   └── PredicateEditModel.cs               # NEW: form model for edit screen
├── Queries/
│   ├── SyncSettingsQuery.cs                # (existing, Phase 8)
│   ├── PredicateListQuery.cs               # NEW: loads all predicates for list
│   └── PredicateByIndexQuery.cs            # NEW: loads single predicate by index
├── Screens/
│   ├── SyncSettingsEditScreen.cs           # (existing, Phase 8)
│   ├── PredicateListScreen.cs              # NEW: list screen
│   └── PredicateEditScreen.cs              # NEW: edit screen
└── Tree/
    ├── SyncSettingsNodeProvider.cs          # MODIFY: add NodeAction to Predicates sub-node
    ├── SyncNavigationNodePathProvider.cs    # MODIFY: add Predicates breadcrumb
    └── PredicateNavigationNodePathProvider.cs # NEW: breadcrumb for predicate screens
```

### Pattern 1: Index-Based Identity for CQRS

**What:** Since predicates have no DB IDs, use array index as the identity key. Pass `Index` as a property on queries and commands.

**When to use:** Any entity stored as a JSON array element without a unique database-assigned ID.

**Example:**
```csharp
// Query: load predicate by array index
public sealed class PredicateByIndexQuery : DataQueryModelBase<PredicateEditModel>
{
    public int Index { get; set; } = -1; // -1 = new predicate

    public override PredicateEditModel? GetModel()
    {
        if (Index < 0)
            return new PredicateEditModel(); // blank form for "add"

        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null) return null;

        var config = ConfigLoader.Load(configPath);
        if (Index >= config.Predicates.Count) return null;

        var pred = config.Predicates[Index];
        return new PredicateEditModel
        {
            Index = Index,
            Name = pred.Name,
            PageId = /* resolved from path */,
            AreaId = pred.AreaId,
            Excludes = string.Join("\n", pred.Excludes)
        };
    }
}
```

### Pattern 2: Page ID <-> Path Conversion

**What:** The `SelectorBuilder.CreatePageSelector()` works with `int` page IDs. The `PredicateDefinition.Path` stores a string breadcrumb path (e.g., "/Customer Center"). The edit model must store both: `PageId` (int, for the selector) and resolve to `Path` (string) on save.

**When to use:** Any time a page selector feeds a string-path model.

**Critical conversion code:**
```csharp
// Save: page ID -> path string
var page = Services.Pages.GetPage(model.PageId);
if (page == null)
    return new() { Status = CommandResult.ResultType.Invalid, Message = "Selected page not found" };
var path = page.GetBreadcrumbPath(); // returns "/Parent/Child"

// Load: path string -> page ID (for edit pre-selection)
// This is harder -- need to walk the content tree or search by path
// Option A: Store PageId alongside Path in PredicateDefinition (recommended)
// Option B: Search pages in the area by breadcrumb path (fragile)
```

**IMPORTANT DESIGN DECISION:** The current `PredicateDefinition` has only `Path` (string), not `PageId` (int). For the page selector to work on edit (pre-selecting the correct page), we need to either:
1. **Add `PageId` to PredicateDefinition** -- store both the int ID and the string path. The path is used by `ContentPredicate.ShouldInclude()` for runtime matching, and PageId is used by the UI selector for pre-selection.
2. **Search pages by path** -- walk `Services.Pages` to find a page matching the breadcrumb path. This is fragile if pages are renamed.

**Recommendation:** Add `PageId` (int, optional, default 0) to `PredicateDefinition`. On save, always compute Path from PageId. On load, use PageId for selector pre-selection. If PageId is 0 (legacy config), the selector starts unselected but the path still works for runtime matching.

### Pattern 3: List Query Returning DataListViewModel

**What:** The list screen's query returns a `DataListViewModel<PredicateListModel>` with the `Data` property populated.

**Example:**
```csharp
public sealed class PredicateListQuery : DataQueryModelBase<DataListViewModel<PredicateListModel>>
{
    public override DataListViewModel<PredicateListModel>? GetModel()
    {
        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null)
            return new DataListViewModel<PredicateListModel>();

        var config = ConfigLoader.Load(configPath);
        var items = config.Predicates.Select((p, i) => new PredicateListModel
        {
            Index = i,
            Name = p.Name,
            Path = p.Path,
            AreaName = Services.Areas.GetArea(p.AreaId)?.Name ?? $"Area {p.AreaId}"
        });

        return new DataListViewModel<PredicateListModel>
        {
            Data = items,
            TotalCount = config.Predicates.Count
        };
    }
}
```

### Pattern 4: SelectorBuilder for Page and Area Pickers

**What:** DW provides `SelectorBuilder.CreatePageSelector()` and `SelectorBuilder.CreateAreaSelector()` as static factory methods. These return `Selector` editor instances that render the full tree picker / dropdown UI.

**How to wire in EditScreenBase:**
```csharp
protected override EditorBase? GetEditor(string property) => property switch
{
    nameof(Model.PageId) => SelectorBuilder.CreatePageSelector(
        value: Model?.PageId > 0 ? Model.PageId : null,
        areaId: Model?.AreaId > 0 ? Model.AreaId : null,
        hint: "Select the root page for this predicate"
    ),
    nameof(Model.AreaId) => SelectorBuilder.CreateAreaSelector(
        value: Model?.AreaId > 0 ? Model.AreaId : null,
        hideDeactivated: true
    ).WithReloadOnChange(),  // Reload screen when area changes so page selector updates
    _ => null
};
```

**Key detail:** `WithReloadOnChange()` on the area selector causes the edit screen to reload when the area is changed, so the page selector can filter to the correct area's content tree.

### Pattern 5: Area Name Resolution

**What:** `Services.Areas.GetArea(int areaId)` returns an `Area` object with `Name` and `DisplayName` properties. `Services.Areas.GetAreas()` returns all areas.

**For list column display:**
```csharp
AreaName = Services.Areas.GetArea(pred.AreaId)?.Name ?? $"Unknown Area ({pred.AreaId})"
```

**For area dropdown (via SelectorBuilder):** Already handled internally by `SelectorBuilder.CreateAreaSelector()`.

### Anti-Patterns to Avoid
- **Free-text Path field:** User explicitly rejected this (D-09). MUST use `SelectorBuilder.CreatePageSelector()`.
- **DB-style auto-increment IDs:** Predicates live in a JSON array. Use array index, not generated IDs.
- **Custom tree picker implementation:** DW already has `SelectorBuilder.CreatePageSelector()`. Do NOT build a custom tree picker.
- **Modifying ConfigLoader validation to require predicates:** D-15 allows zero predicates. ConfigLoader currently throws if predicates is empty -- this must be fixed.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Content tree page picker | Custom tree view or free-text input | `SelectorBuilder.CreatePageSelector()` | Full tree navigation, search, area filtering built in |
| Area dropdown | Custom Select with manual option loading | `SelectorBuilder.CreateAreaSelector()` | Handles area enumeration, deactivated areas, search |
| Delete confirmation dialog | Custom modal / JS confirm | `ActionBuilder.Delete(command, title, message)` | Standard DW confirmation UX |
| List-to-edit navigation | Manual URL construction | `NavigateScreenAction.To<EditScreen>().With(query)` | Framework handles screen routing |
| Edit-to-list navigation on save | Custom redirect logic | `CommandResult` with `Status = Ok` | Framework returns to list automatically on success |

**Key insight:** DW's CoreUI framework handles nearly all UI plumbing (navigation, confirmation dialogs, form validation display, list rendering). The implementation is primarily about wiring data models to framework base classes.

## Common Pitfalls

### Pitfall 1: ConfigLoader Rejects Zero Predicates
**What goes wrong:** D-15 says deleting the last predicate is valid. But `ConfigLoader.Load()` at line 48-49 throws `InvalidOperationException` when `Predicates` is null or empty.
**Why it happens:** Original design assumed at least one predicate always exists.
**How to avoid:** Modify ConfigLoader validation to allow empty predicates list. Change the check to only validate predicate fields when predicates exist. Make `Predicates` default to empty list instead of throwing.
**Warning signs:** Delete command succeeds but subsequent screen loads crash.

### Pitfall 2: Page ID vs Path Mismatch
**What goes wrong:** Page selector returns int ID, but PredicateDefinition stores string Path. If only the Path is stored, the edit screen cannot pre-select the correct page in the tree picker.
**Why it happens:** PredicateDefinition was designed for runtime path matching, not UI round-tripping.
**How to avoid:** Add optional `PageId` (int) to PredicateDefinition. Store both on save. Use PageId for selector, Path for runtime matching.
**Warning signs:** Edit screen opens with empty page selector even though a path is configured.

### Pitfall 3: Index Shift After Delete
**What goes wrong:** After deleting predicate at index 2, the predicate that was at index 3 is now at index 2. If the list screen is not refreshed, context menu actions point to wrong predicates.
**Why it happens:** Array indices shift when elements are removed.
**How to avoid:** Delete command writes to config and returns `CommandResult.Ok`. The framework reloads the list screen, which re-queries from disk with correct indices. No caching of indices client-side.
**Warning signs:** Editing after deleting modifies the wrong predicate.

### Pitfall 4: Concurrent Config File Access
**What goes wrong:** A scheduled task reads the config file while the admin UI is writing to it.
**Why it happens:** No file-level locking between UI and background tasks.
**How to avoid:** ConfigWriter already uses atomic temp+rename pattern (write to `.tmp`, then `File.Move` with overwrite). Readers always get a complete file. This is already handled.
**Warning signs:** JSON parse errors in scheduled task logs.

### Pitfall 5: Excludes Newline Handling
**What goes wrong:** Textarea returns `\r\n` on Windows, but Path matching expects clean paths.
**Why it happens:** Windows line endings in textarea value.
**How to avoid:** When splitting Excludes textarea to `List<string>`, use `Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)` and `Trim()` each entry.
**Warning signs:** Exclude rules don't match because paths have trailing `\r`.

### Pitfall 6: SelectorBuilder Returns Empty Selector When Provider Not Found
**What goes wrong:** `SelectorBuilder.CreatePageSelector()` calls `TryGetSelectorAndProvider<IPageSelectorProvider, int>()` which uses `AddInManager.GetInstance<IPageSelectorProvider>()`. If `Dynamicweb.Content.UI` is not properly referenced/loaded, it returns an empty `Selector()`.
**Why it happens:** The `PageSelectorProvider` class is in `Dynamicweb.Content.UI` assembly, registered via AddInManager at runtime.
**How to avoid:** Ensure `Dynamicweb.Content.UI` NuGet package is referenced (already done in Phase 7). Verify at runtime that the selector has a non-null Provider.
**Warning signs:** Page field renders as empty box with no tree picker.

## Code Examples

### List Screen (verified from ExpressDelivery + DW10 source)
```csharp
// Source: ExpressDeliveryPresetListScreen.cs pattern
public sealed class PredicateListScreen : ListScreenBase<PredicateListModel>
{
    protected override string GetScreenName() => "Content Sync Predicates";

    protected override IEnumerable<ListViewMapping> GetViewMappings() =>
    [
        new RowViewMapping
        {
            Columns =
            [
                CreateMapping(m => m.Name),
                CreateMapping(m => m.Path),
                CreateMapping(m => m.AreaName)
            ]
        }
    ];

    protected override ActionBase GetListItemPrimaryAction(PredicateListModel model) =>
        NavigateScreenAction.To<PredicateEditScreen>()
            .With(new PredicateByIndexQuery { Index = model.Index });

    protected override IEnumerable<ActionGroup>? GetListItemContextActions(PredicateListModel model) =>
    [
        new()
        {
            Nodes =
            [
                ActionBuilder.Edit<PredicateEditScreen>(new PredicateByIndexQuery { Index = model.Index }),
                ActionBuilder.Delete(
                    new DeletePredicateCommand { Index = model.Index },
                    "Delete predicate?",
                    $"Are you sure you want to delete predicate '{model.Name}'?")
            ]
        }
    ];

    protected override ActionNode GetItemCreateAction() =>
        new()
        {
            Name = "New predicate",
            Icon = Icon.PlusSquare,
            NodeAction = NavigateScreenAction.To<PredicateEditScreen>()
        };
}
```

### Edit Screen with Selectors (verified from PathEditScreen + AreaEditScreen patterns)
```csharp
// Source: PathEditScreen.cs GetEditor pattern + SelectorBuilder API
public sealed class PredicateEditScreen : EditScreenBase<PredicateEditModel>
{
    protected override void BuildEditScreen()
    {
        AddComponents("Predicate",
        [
            new("Configuration",
            [
                EditorFor(m => m.Name),
                EditorFor(m => m.AreaId),    // Area selector (renders first so reload updates page picker)
                EditorFor(m => m.PageId),    // Page tree picker
                EditorFor(m => m.Excludes)   // Multi-line textarea
            ])
        ]);
    }

    protected override EditorBase? GetEditor(string property) => property switch
    {
        nameof(Model.AreaId) => SelectorBuilder.CreateAreaSelector(
            value: Model?.AreaId > 0 ? Model.AreaId : null,
            hideDeactivated: true
        ).WithReloadOnChange(),
        nameof(Model.PageId) => SelectorBuilder.CreatePageSelector(
            value: Model?.PageId > 0 ? Model.PageId : null,
            areaId: Model?.AreaId > 0 ? Model.AreaId : null,
            hint: "Select root page for this predicate"
        ),
        nameof(Model.Excludes) => new Textarea
        {
            Label = "Excludes",
            Explanation = "One path per line. Pages under these paths will be excluded from sync."
        },
        _ => null
    };

    protected override string GetScreenName() => Model?.Index >= 0 ? $"Edit Predicate: {Model.Name}" : "New Predicate";
    protected override CommandBase<PredicateEditModel> GetSaveCommand() => new SavePredicateCommand();
}
```

### Delete Command (verified from ExpressDelivery pattern)
```csharp
// Source: DeleteExpressDeliveryPresetCommand.cs pattern
public sealed class DeletePredicateCommand : CommandBase
{
    public int Index { get; set; }

    public override CommandResult Handle()
    {
        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null)
            return new() { Status = CommandResult.ResultType.Error, Message = "Config file not found" };

        var config = ConfigLoader.Load(configPath);
        if (Index < 0 || Index >= config.Predicates.Count)
            return new() { Status = CommandResult.ResultType.Error, Message = "Invalid predicate index" };

        var predicates = config.Predicates.ToList();
        predicates.RemoveAt(Index);

        var updated = config with { Predicates = predicates };
        ConfigWriter.Save(updated, configPath);

        return new() { Status = CommandResult.ResultType.Ok };
    }
}
```

### Area Name Resolution (verified from DW10 source)
```csharp
// Source: PathMappingConfiguration.cs, TranslationFieldProvider.cs
// Services.Areas is Dynamicweb.Content.Services.Areas (static API)
var area = Services.Areas.GetArea(areaId);   // returns Area or null
var areaName = area?.Name ?? "Unknown";      // Area.Name is the display name

// Get all areas (for validation or iteration)
var allAreas = Services.Areas.GetAreas();    // returns IEnumerable<Area>
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Custom tree picker HTML | `SelectorBuilder.CreatePageSelector()` | DW10 | All page selection uses centralized selector API |
| Manual Select options for areas | `SelectorBuilder.CreateAreaSelector()` | DW10 | Area enumeration handled by framework |
| Custom confirmation JS | `ActionBuilder.Delete()` | DW10 | Standardized delete confirmation |
| `GetCustomEditor()` override | `GetEditor()` override | DW10 (renamed) | Same functionality, cleaner name |

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 + Moq 4.20.72 |
| Config file | `tests/Dynamicweb.ContentSync.Tests/Dynamicweb.ContentSync.Tests.csproj` |
| Quick run command | `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "FullyQualifiedName~AdminUI" --no-build` |
| Full suite command | `dotnet test tests/Dynamicweb.ContentSync.Tests --no-build` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PRED-01 | Predicates node has NodeAction wired | manual-only | Runtime verification in DW instance | N/A |
| PRED-02 | List query returns correct predicates | unit | `dotnet test --filter "FullyQualifiedName~PredicateListQuery" --no-build` | No -- Wave 0 |
| PRED-03 | Save command creates new predicate | unit | `dotnet test --filter "FullyQualifiedName~SavePredicateCommand" --no-build` | No -- Wave 0 |
| PRED-04 | Save command updates existing predicate by index | unit | `dotnet test --filter "FullyQualifiedName~SavePredicateCommand" --no-build` | No -- Wave 0 |
| PRED-05 | Delete command removes predicate by index | unit | `dotnet test --filter "FullyQualifiedName~DeletePredicateCommand" --no-build` | No -- Wave 0 |
| PRED-06 | Round-trip: save then load preserves predicates | unit | `dotnet test --filter "FullyQualifiedName~ConfigLoader" --no-build` | Partial (existing ConfigLoader tests) |

**Manual-only justification for PRED-01:** Tree node wiring is purely declarative (single line change) and requires a running DW instance to verify visually. No unit-testable logic.

### Sampling Rate
- **Per task commit:** `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "FullyQualifiedName~AdminUI" --no-build`
- **Per wave merge:** `dotnet test tests/Dynamicweb.ContentSync.Tests --no-build`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Dynamicweb.ContentSync.Tests/AdminUI/SavePredicateCommandTests.cs` -- covers PRED-03, PRED-04, PRED-06
- [ ] `tests/Dynamicweb.ContentSync.Tests/AdminUI/DeletePredicateCommandTests.cs` -- covers PRED-05
- [ ] `tests/Dynamicweb.ContentSync.Tests/Configuration/ConfigLoaderZeroPredicatesTests.cs` -- covers D-15 (allow zero predicates)

## Open Questions

1. **Page ID reverse lookup on edit load**
   - What we know: PageSelector uses int page IDs. PredicateDefinition stores string Path. We need to pre-select the correct page when editing.
   - What's unclear: Whether adding PageId to PredicateDefinition is acceptable or if we should search pages by breadcrumb path.
   - Recommendation: Add optional `PageId` property to `PredicateDefinition` (int, default 0). This is the cleanest approach and allows legacy configs (without PageId) to still work. The planner should include this as a task.

2. **Breadcrumb path stability**
   - What we know: `Page.GetBreadcrumbPath()` returns "/" + ancestor MenuText chain. If a page is renamed, the path changes.
   - What's unclear: Whether predicates should auto-update when pages are renamed.
   - Recommendation: Do NOT auto-update. The predicate stores a path snapshot. If a page is renamed, the predicate path becomes stale (won't match). This is acceptable for v1.2 -- user can re-select the page in the edit screen.

3. **NavigationNodePathProvider for predicate screens**
   - What we know: Phase 8 has `SyncNavigationNodePathProvider` for the settings screen breadcrumb. Predicate screens need a similar provider.
   - What's unclear: Whether we need separate providers for list and edit, or one provider for both.
   - Recommendation: One provider for the predicate list model that extends the Sync breadcrumb path with the Predicates node ID.

## Sources

### Primary (HIGH confidence)
- `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Editors\Selectors\SelectorBuilder.cs` -- `CreatePageSelector()` and `CreateAreaSelector()` APIs, full implementation
- `C:\Projects\temp\dw10source\Dynamicweb.Content.UI\Selectors\PageSelectorProvider.cs` -- `PageSelectorProvider` implementation showing page ID selection and area filtering
- `C:\Projects\temp\dwextensionsample\Samples-main\ExpressDelivery\` -- Complete CRUD pattern (ListScreen, EditScreen, Commands, Queries, Models)
- `C:\Projects\temp\dw10source\Dynamicweb.Application.UI\Screens\PathEditScreen.cs` -- Real-world usage of `SelectorBuilder.CreateAreaSelector().WithReloadOnChange()`
- `C:\VibeCode\Dynamicweb.ContentSync\src\Dynamicweb.ContentSync\` -- Existing ConfigLoader, ConfigWriter, SyncSettingsNodeProvider, Phase 8 screens

### Secondary (MEDIUM confidence)
- `C:\Projects\temp\dw10source\Dynamicweb.Content.UI\Api\AreaServiceHelper.cs` -- `Services.Areas.SaveArea()` usage pattern
- `C:\Projects\temp\dw10source\Dynamicweb.Application.UI\` -- `Services.Areas.GetAreas()`, `Services.Areas.GetArea(id)` usage across multiple files

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all components verified in DW10 source code with working examples
- Architecture: HIGH -- patterns directly observed in ExpressDelivery sample and Phase 8 implementation
- Pitfalls: HIGH -- ConfigLoader validation conflict verified by reading source; page ID mismatch identified from code analysis
- Page selector: HIGH -- `SelectorBuilder.CreatePageSelector()` found with 20+ usage sites in DW10 source
- Area API: HIGH -- `Services.Areas.GetArea(id)` / `GetAreas()` found with 20+ usage sites in DW10 source

**Research date:** 2026-03-22
**Valid until:** 2026-04-22 (stable -- DW10 UI framework is mature)
