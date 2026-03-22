---
phase: 09-predicate-management
verified: 2026-03-22T00:30:00Z
status: human_needed
score: 5/5 must-haves verified (automated), 1 item needs human
re_verification: false
human_verification:
  - test: "Edit screen breadcrumb shows correct path"
    expected: "When on the PredicateEditScreen, the breadcrumb should show Settings > Content > Sync > Predicates"
    why_human: "PredicateNavigationNodePathProvider is typed to PredicateListModel only. DW resolves breadcrumbs by model type match. The edit screen uses PredicateEditModel, and no PredicateEditNavigationNodePathProvider exists. Whether DW falls back to the list provider or shows a broken/empty breadcrumb requires runtime verification. Documented in 09-02-SUMMARY as a potential concern."
---

# Phase 09: Predicate Management Verification Report

**Phase Goal:** Users can manage content sync predicates (add, view, edit, delete) from the DW admin UI
**Verified:** 2026-03-22T00:30:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | A "Predicates" sub-node appears under the Sync node in admin navigation and opens a list screen | VERIFIED | `SyncSettingsNodeProvider.cs` yields `PredicatesNodeId` node under `SyncNodeId` with `NavigateScreenAction.To<PredicateListScreen>().With(new PredicateListQuery())` |
| 2 | User can view all configured predicates showing their name, path, and include/exclude status | VERIFIED | `PredicateListScreen` maps columns Name, Path, AreaName via `RowViewMapping`; `PredicateListQuery` loads from `ConfigLoader.Load` and resolves area names via `Services.Areas.GetArea` |
| 3 | User can add a new predicate, edit an existing predicate, and delete a predicate from the list screen | VERIFIED | Add: `GetItemCreateAction` navigates to blank `PredicateEditScreen`; Edit: `ActionBuilder.Edit<PredicateEditScreen>` in context menu; Delete: `ActionBuilder.Delete` with `DeletePredicateCommand` and confirmation dialog including predicate name |
| 4 | All predicate changes persist to ContentSync.config.json and survive a screen reload | VERIFIED | `SavePredicateCommand.Handle()` and `DeletePredicateCommand.Handle()` both call `ConfigWriter.Save(updated, configPath)`; queries always call `ConfigLoader.Load(configPath)` on each invocation (no caching); 10 unit tests confirm disk persistence |
| 5 | Predicates added via the admin UI are respected by the next scheduled task serialization run | VERIFIED (structural) | `SerializeScheduledTask.Run()` calls `ConfigLoader.Load(configPath)` fresh at each execution; `ContentSerializer` iterates `config.Predicates`; human verification required for runtime behavior |

**Score:** 5/5 truths verified (1 human verification item)

### Required Artifacts

**Plan 01 artifacts:**

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Dynamicweb.ContentSync/Configuration/PredicateDefinition.cs` | PageId property added | VERIFIED | `public int PageId { get; init; } = 0` at line 8 |
| `src/Dynamicweb.ContentSync/Configuration/ConfigLoader.cs` | Zero predicates allowed | VERIFIED | Line 48-49: `if (raw.Predicates is null) raw.Predicates = new List<RawPredicateDefinition>();` — no longer rejects empty |
| `src/Dynamicweb.ContentSync/AdminUI/Models/PredicateListModel.cs` | Row model for list screen | VERIFIED | `public sealed class PredicateListModel : DataViewModelBase` with Index, Name, Path, AreaName |
| `src/Dynamicweb.ContentSync/AdminUI/Models/PredicateEditModel.cs` | Form model for edit screen | VERIFIED | `public sealed class PredicateEditModel : DataViewModelBase` with `Index = -1` sentinel, Name, AreaId, PageId, Excludes |
| `src/Dynamicweb.ContentSync/AdminUI/Queries/PredicateListQuery.cs` | List data query | VERIFIED | `DataQueryModelBase<DataListViewModel<PredicateListModel>>` calling `ConfigLoader.Load` and `Services.Areas.GetArea` |
| `src/Dynamicweb.ContentSync/AdminUI/Queries/PredicateByIndexQuery.cs` | Single predicate query by index | VERIFIED | Returns blank `PredicateEditModel()` for `Index < 0` |
| `src/Dynamicweb.ContentSync/AdminUI/Commands/SavePredicateCommand.cs` | Save/create command | VERIFIED | Handles create (Index=-1), update, duplicate name check, `\r\n` excludes splitting, `ConfigWriter.Save` |
| `src/Dynamicweb.ContentSync/AdminUI/Commands/DeletePredicateCommand.cs` | Delete command | VERIFIED | Index validation, `predicates.RemoveAt(Index)`, `ConfigWriter.Save` |
| `tests/Dynamicweb.ContentSync.Tests/AdminUI/PredicateCommandTests.cs` | Save/delete unit tests | VERIFIED | 10 test methods (`[Fact]`), 273 lines — all pass |

**Plan 02 artifacts:**

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Dynamicweb.ContentSync/AdminUI/Screens/PredicateListScreen.cs` | List screen with RowViewMapping | VERIFIED | `ListScreenBase<PredicateListModel>`, columns Name/Path/AreaName, primary action, context actions (Edit + Delete), Add button |
| `src/Dynamicweb.ContentSync/AdminUI/Screens/PredicateEditScreen.cs` | Edit screen with selectors | VERIFIED | `EditScreenBase<PredicateEditModel>`, area selector with `WithReloadOnChange`, page selector, `Textarea` for excludes, `new SavePredicateCommand()` |
| `src/Dynamicweb.ContentSync/AdminUI/Tree/SyncSettingsNodeProvider.cs` | Predicates node with NodeAction wired | VERIFIED | `NavigateScreenAction.To<PredicateListScreen>().With(new PredicateListQuery())` — placeholder comment removed |
| `src/Dynamicweb.ContentSync/AdminUI/Tree/PredicateNavigationNodePathProvider.cs` | Breadcrumb for predicate screens | VERIFIED | `NavigationNodePathProvider<PredicateListModel>` with path through SettingsArea, AreasSection, Content_Settings, SyncNodeId, PredicatesNodeId |

### Key Link Verification

**Plan 01 key links:**

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `SavePredicateCommand.cs` | `ConfigWriter.Save` | load-modify-save cycle | WIRED | Line 91: `ConfigWriter.Save(updated, configPath)` |
| `DeletePredicateCommand.cs` | `ConfigWriter.Save` | load-remove-save cycle | WIRED | Line 29: `ConfigWriter.Save(updated, configPath)` |
| `PredicateListQuery.cs` | `ConfigLoader.Load` | fresh disk read | WIRED | Line 17: `var config = ConfigLoader.Load(configPath)` |

**Plan 02 key links:**

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `PredicateListScreen.cs` | `PredicateEditScreen` | `NavigateScreenAction.To<PredicateEditScreen>` | WIRED | Line 32 (primary action) and line 41 (context Edit action) |
| `PredicateListScreen.cs` | `DeletePredicateCommand` | `ActionBuilder.Delete` | WIRED | Line 42: `ActionBuilder.Delete(new DeletePredicateCommand { Index = model.Index }, ...)` |
| `PredicateEditScreen.cs` | `SavePredicateCommand` | `GetSaveCommand` | WIRED | Line 49: `protected override CommandBase<PredicateEditModel> GetSaveCommand() => new SavePredicateCommand()` |
| `PredicateEditScreen.cs` | `SelectorBuilder` | `GetEditor` override | WIRED | Lines 29 and 33: `SelectorBuilder.CreateAreaSelector` and `SelectorBuilder.CreatePageSelector` |
| `SyncSettingsNodeProvider.cs` | `PredicateListScreen` | `NodeAction` | WIRED | Line 45: `NavigateScreenAction.To<PredicateListScreen>().With(new PredicateListQuery())` |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| PRED-01 | 09-02 | Predicates sub-node under Sync node in admin navigation | SATISFIED | `SyncSettingsNodeProvider` yields Predicates node under SyncNodeId; wired to `PredicateListScreen` |
| PRED-02 | 09-01, 09-02 | User can view list of configured predicates (name, path, include/exclude) | SATISFIED | `PredicateListScreen` with Name, Path, AreaName columns; `PredicateListQuery` reads from config |
| PRED-03 | 09-01, 09-02 | User can add a new predicate | SATISFIED | Add button via `GetItemCreateAction`; `SavePredicateCommand` with Index=-1 appends new predicate |
| PRED-04 | 09-01, 09-02 | User can edit an existing predicate | SATISFIED | Edit context action; `SavePredicateCommand` with valid Index updates at that position |
| PRED-05 | 09-01, 09-02 | User can delete a predicate | SATISFIED | Delete context action with confirmation dialog; `DeletePredicateCommand.RemoveAt(Index)` |
| PRED-06 | 09-01 | Predicate changes persist to ContentSync.config.json | SATISFIED | Both Save and Delete commands call `ConfigWriter.Save`; unit tests confirm disk round-trip |

No orphaned requirements — all six PRED IDs are accounted for across plans 01 and 02.

### Anti-Patterns Found

No anti-patterns detected. Scanned all 9 phase-created/modified source files:

- No TODO/FIXME/HACK/PLACEHOLDER comments remaining (the "Phase 9 will add NodeAction here" placeholder was removed)
- No stub return patterns (`return null`, `return {}`, `return []` as final outputs)
- No empty handlers — all commands perform real load-modify-save operations
- `SavePredicateCommand` try-catch around `Services.Pages` is a documented and intentional testability pattern, not a stub

### Human Verification Required

#### 1. Edit Screen Breadcrumb Resolution

**Test:** Open the DW admin, navigate to Settings > Content > Sync > Predicates. Click any predicate row (or Add) to open the edit screen. Observe the breadcrumb.
**Expected:** Breadcrumb shows: Settings > Content > Sync > Predicates (i.e., highlights the Predicates node in the tree)
**Why human:** `PredicateNavigationNodePathProvider` is typed to `PredicateListModel`. DW resolves breadcrumbs by matching the screen's model type to a registered `NavigationNodePathProvider<T>`. The edit screen uses `PredicateEditModel`. No `PredicateEditNavigationNodePathProvider` exists. Whether DW uses the list provider as a fallback, resolves to a parent node, or shows no highlight requires runtime observation. The SUMMARY documents this as a "potential concern."

If breadcrumb is broken on the edit screen: add a second provider class `PredicateEditNavigationNodePathProvider : NavigationNodePathProvider<PredicateEditModel>` with identical path logic to the existing list provider.

### Gaps Summary

No automated gaps found. All artifacts exist, are substantive, and are correctly wired. All 97 tests pass (build: 0 errors). The single open item is a runtime/visual concern about edit screen breadcrumb resolution that cannot be verified without a running DW instance.

---

_Verified: 2026-03-22T00:30:00Z_
_Verifier: Claude (gsd-verifier)_
