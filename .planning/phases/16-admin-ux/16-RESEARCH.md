# Phase 16: Admin UX - Research

**Researched:** 2026-03-24
**Domain:** DynamicWeb admin UI (tree nodes, screen injectors, log viewer), project rename
**Confidence:** HIGH

## Summary

Phase 16 has two distinct waves: Wave 1 is a full project rename from `Dynamicweb.ContentSync` to `DynamicWeb.Serializer` (REN-01), and Wave 2 implements four Admin UX requirements (UX-01 through UX-04). The rename is mechanical but wide-reaching -- 130 occurrences across 63 source files, 96 occurrences across 29 test files, plus solution/project files and config file naming.

The UX work builds on well-established DynamicWeb admin patterns already used in the project: `NavigationNodeProvider` for tree nodes, `EditScreenBase` for settings screens, and `ScreenInjector` for action injection. The key technical discovery is that `FileOverviewScreen` extends `OverviewScreenBase` (not `EditScreenBase`), so the zip file injector must use `ScreenInjector<FileOverviewScreen>` with `OnAfter()` to inject actions into the `ScreenLayout.ContextActionGroups`, following the same pattern as `ProductOverviewScreenInjector` in DW's ecommerce module.

**Primary recommendation:** Execute Wave 1 (rename) as a single atomic commit before any UX work. Then Wave 2 can implement UX features using the new namespace without double-touching files.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Full rename: namespace `Dynamicweb.ContentSync` -> `DynamicWeb.Serializer`, assembly `DynamicWeb.Serializer.dll`, NuGet package `DynamicWeb.Serializer`
- **D-02:** Note the casing: `DynamicWeb` (capital W) -- matches the PROJECT.md milestone name, NOT DW's own lowercase convention
- **D-03:** Rename covers: namespaces, assembly name, csproj, NuGet metadata, test project, all `using` statements, config file references
- **D-04:** API command names change: `ContentSyncSerialize` -> `SerializerSerialize`, `ContentSyncDeserialize` -> `SerializerDeserialize` (no backward-compat aliases needed)
- **D-05:** This is Wave 1 -- lands and stabilizes before any UX work begins in Wave 2
- **D-06:** Move admin tree from Settings > Content > Sync to Settings > Database > Serialize
- **D-07:** User-facing label: "Serialize" (not "Serializer" or "Database Sync")
- **D-08:** Change `SyncSettingsNodeProvider` parent from `Content_Settings` to `Settings_Database`
- **D-09:** Update `SyncNavigationNodePathProvider` and `PredicateNavigationNodePathProvider` breadcrumbs to route through `SystemSection` / `Settings_Database`
- **D-10:** Screen title changes from "Content Sync Settings" to "Serialize Settings"
- **D-11:** Separate log files per run, named with operation type + timestamp: `Serialize_2026-03-24_143052.log` / `Deserialize_2026-03-24_143052.log`
- **D-12:** Log file content: existing timestamped text format with a structured JSON summary block prepended at the top -- viewer parses the header, humans can read the rest
- **D-13:** Log viewer lives as a sub-node under Serialize: Settings > Database > Serialize > Log Viewer
- **D-14:** Viewer shows a dropdown/list of available log files, always starts with the latest run selected
- **D-15:** Guided advice = error-specific suggestions, not just summaries
- **D-16:** No auto-cleanup -- log files accumulate, user can delete from disk if needed
- **D-17:** Data source: parse log files from disk (no in-memory OrchestratorResult capture needed)
- **D-18:** Inject "Import to database" action on `FileOverviewScreen` for .zip files -- gated to zips in the configured output directory only
- **D-19:** Flow: click action -> auto dry-run -> show per-table breakdown -> user confirms -> execute -> redirect to log viewer
- **D-20:** Zip extracted to temp directory, cleaned up after deserialization completes
- **D-21:** If zip contains no valid YAML files matching configured predicates, fail fast
- **D-22:** Injector class: `SerializerFileOverviewInjector : EditScreenInjector<FileOverviewScreen, FileDataModel>`, gated on `Model?.Extension == ".zip"` AND file is in output directory
- **D-23:** Scheduled tasks already deleted (commit `a32703f`). Only remaining work: update README to document API commands as the replacement, formally close UX-04.

### Claude's Discretion
- Log viewer screen layout and styling within DW admin conventions
- Advice rule catalog -- determine based on common error patterns during implementation
- Exact JSON structure for the log file summary header
- How to detect "output directory" for the zip file path check in the injector
- Test strategy for the new screens and injector

### Deferred Ideas (OUT OF SCOPE)
- Settings & Schema providers -- future milestone scope
- Users, Marketing, PIM, Apps tables -- future milestone scope
- DataGroup auto-discovery -- future, enumerate available tables from DW metadata
- Batch predicate config (one predicate = all tables in a DataGroup) -- future UX improvement
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| REN-01 | Project renamed from Dynamicweb.ContentSync to DynamicWeb.Serializer (namespace, assembly, NuGet package) | Runtime State Inventory (rename scope), file count analysis, config file naming |
| UX-01 | Log viewer screen shows per-provider summaries with guided advice | OrchestratorResult/ProviderDeserializeResult structure, EditScreenBase pattern, JSON summary header design |
| UX-02 | Deserialize action available on Asset management file detail page for zip files | FileOverviewScreen/OverviewScreenBase analysis, ScreenInjector pattern discovery, Dynamicweb.Files.UI package |
| UX-03 | Admin tree node relocated from Settings > Content > Sync to Settings > Database > Serialize | SystemNodeProvider analysis, Settings_Database node ID, SystemSection vs AreasSection |
| UX-04 | Scheduled tasks deprecated (API commands are the replacement) | Already deleted (commit a32703f), README update only |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Dynamicweb | 10.23.9 | Core DW platform APIs | Already in use, pinned version |
| Dynamicweb.Content.UI | 10.23.9 | Content tree UI types (PageEditScreen, PageDataModel) | Already in use |
| Dynamicweb.CoreUI.Rendering | 10.23.9 | Admin screen rendering pipeline | Already in use |
| Dynamicweb.Files.UI | 10.23.9 | FileOverviewScreen, FileDataModel for zip injector | Transitive dep already resolved, needs explicit reference for UX-02 |
| YamlDotNet | 13.7.1 | YAML serialization | Already in use |
| System.Text.Json | (built-in) | JSON summary header in log files | .NET 8 built-in, no extra dependency |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.IO.Compression | (built-in) | Zip extraction for UX-02 deserialization flow | Already referenced in existing DeserializeCommand |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| System.Text.Json for log header | Newtonsoft.Json | STJ is built-in, no dep needed. DW uses both internally, but STJ is lighter |

**Installation:**
```bash
# Add Dynamicweb.Files.UI package reference for UX-02 injector
dotnet add src/DynamicWeb.Serializer/DynamicWeb.Serializer.csproj package Dynamicweb.Files.UI --version 10.23.9
```

## Architecture Patterns

### Recommended Project Structure (post-rename)
```
src/
  DynamicWeb.Serializer/
    AdminUI/
      Commands/
        SerializerSerializeCommand.cs      # renamed from ContentSyncSerializeCommand
        SerializerDeserializeCommand.cs     # renamed from ContentSyncDeserializeCommand
        DeserializeFromZipCommand.cs        # NEW: UX-02 zip deserialization
        SaveSerializerSettingsCommand.cs    # renamed from SaveSyncSettingsCommand
        SavePredicateCommand.cs
        DeletePredicateCommand.cs
        SerializeSubtreeCommand.cs
      Injectors/
        SerializerPageEditInjector.cs       # renamed from ContentSyncPageEditInjector
        SerializerFileOverviewInjector.cs   # NEW: UX-02 zip action injection
      Models/
        SerializerSettingsModel.cs          # renamed from SyncSettingsModel
        PredicateEditModel.cs
        PredicateListModel.cs
        LogViewerModel.cs                   # NEW: UX-01
        LogFileSummary.cs                   # NEW: UX-01 JSON header model
      Queries/
        SerializerSettingsQuery.cs          # renamed from SyncSettingsQuery
        PredicateListQuery.cs
        PredicateByIndexQuery.cs
        LogViewerQuery.cs                   # NEW: UX-01
      Screens/
        SerializerSettingsEditScreen.cs     # renamed from SyncSettingsEditScreen
        PredicateListScreen.cs
        PredicateEditScreen.cs
        LogViewerScreen.cs                  # NEW: UX-01
      Tree/
        SerializerSettingsNodeProvider.cs   # renamed from SyncSettingsNodeProvider
        SerializerNavigationNodePathProvider.cs   # renamed
        PredicateNavigationNodePathProvider.cs
        LogViewerNavigationNodePathProvider.cs    # NEW: UX-01
    Configuration/
    Infrastructure/
    Models/
    Providers/
    Serialization/
tests/
  DynamicWeb.Serializer.Tests/
  DynamicWeb.Serializer.IntegrationTests/
```

### Pattern 1: Tree Node Relocation (UX-03)

**What:** Change the `NavigationNodeProvider` from `AreasSection` to `SystemSection` and listen for `Settings_Database` parent ID instead of `Content_Settings`.

**Current code** (SyncSettingsNodeProvider.cs):
```csharp
public sealed class SyncSettingsNodeProvider : NavigationNodeProvider<AreasSection>
{
    private const string ContentRootId = "Content_Settings";
    // ...
    public override IEnumerable<NavigationNode> GetSubNodes(NavigationNodePath parentNodePath)
    {
        if (parentNodePath.Last == ContentRootId)
        {
            yield return new NavigationNode { Id = SyncNodeId, Name = "Sync", ... };
        }
    }
}
```

**New code** (post-rename SerializerSettingsNodeProvider.cs):
```csharp
public sealed class SerializerSettingsNodeProvider : NavigationNodeProvider<SystemSection>
{
    // Settings_Database = "Settings_" + "Database" from SystemNodeProvider
    private const string DatabaseRootId = "Settings_Database";
    internal const string SerializeNodeId = "Serializer_Settings";
    internal const string PredicatesNodeId = "Serializer_Predicates";
    internal const string LogViewerNodeId = "Serializer_LogViewer";

    public override IEnumerable<NavigationNode> GetSubNodes(NavigationNodePath parentNodePath)
    {
        if (parentNodePath.Last == DatabaseRootId)
        {
            yield return new NavigationNode
            {
                Id = SerializeNodeId,
                Name = "Serialize",  // D-07: user-facing label
                Sort = 100,
                HasSubNodes = true,
                NodeAction = NavigateScreenAction.To<SerializerSettingsEditScreen>()
                    .With(new SerializerSettingsQuery())
            };
        }
        else if (parentNodePath.Last == SerializeNodeId)
        {
            yield return new NavigationNode
            {
                Id = PredicatesNodeId, Name = "Predicates", Sort = 10, HasSubNodes = false,
                NodeAction = NavigateScreenAction.To<PredicateListScreen>()
                    .With(new PredicateListQuery())
            };
            yield return new NavigationNode
            {
                Id = LogViewerNodeId, Name = "Log Viewer", Sort = 20, HasSubNodes = false,
                NodeAction = NavigateScreenAction.To<LogViewerScreen>()
                    .With(new LogViewerQuery())
            };
        }
    }
}
```

**Breadcrumb path changes** (NavigationNodePathProviders):
```csharp
// OLD path: SettingsArea -> AreasSection -> Content_Settings -> SyncNodeId
// NEW path: SettingsArea -> SystemSection -> Settings_Database -> SerializeNodeId

protected override NavigationNodePath GetNavigationNodePathInternal(SerializerSettingsModel? model) =>
    new([
        typeof(SettingsArea).FullName,
        NavigationContext.Empty,
        typeof(SystemSection).FullName,   // Changed from AreasSection
        "Settings_Database",               // Changed from Content_Settings
        SerializerSettingsNodeProvider.SerializeNodeId
    ]);
```

### Pattern 2: OverviewScreen Injector (UX-02)

**CRITICAL FINDING:** The CONTEXT.md specifies `EditScreenInjector<FileOverviewScreen, FileDataModel>` (D-22), but `FileOverviewScreen` extends `OverviewScreenBase<FileDataModel>`, NOT `EditScreenBase<FileDataModel>`. The `EditScreenInjector<TScreen, TModel>` has a constraint `where TScreen : EditScreenBase<TModel>`, so using it with `FileOverviewScreen` will not compile.

**Correct approach:** Use `ScreenInjector<FileOverviewScreen>` with `OnAfter()`, following the `ProductOverviewScreenInjector` pattern from DW's ecommerce module.

```csharp
// Source: DW10 source Dynamicweb.Ecommerce.UI/Injectors/ProductOverviewScreenInjector.cs
public sealed class SerializerFileOverviewInjector : ScreenInjector<FileOverviewScreen>
{
    public override void OnAfter(FileOverviewScreen screen, UiComponentBase content)
    {
        var model = Screen?.Model;
        if (model is null)
            return;

        // Gate: only .zip files in the configured output directory
        if (!string.Equals(model.Extension, ".zip", StringComparison.OrdinalIgnoreCase))
            return;

        if (!IsInOutputDirectory(model.FilePath))
            return;

        // Inject "Import to database" action
        content.TryGet<ScreenLayout>(out var screenLayout);
        screenLayout?.ContextActionGroups.Add(new ActionGroup
        {
            Nodes = [
                new ActionNode
                {
                    Name = "Import to database",
                    Icon = Icon.Upload,
                    NodeAction = OpenDialogAction.To<DeserializeFromZipScreen>(
                        new DeserializeFromZipQuery { FilePath = model.FilePath })
                }
            ]
        });
    }

    private static bool IsInOutputDirectory(string filePath)
    {
        // Load config, resolve output directory, check if filePath is under it
        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null) return false;
        var config = ConfigLoader.Load(configPath);
        // Normalize and compare paths
        return filePath.Replace('\\', '/').Contains(
            config.OutputDirectory.Replace('\\', '/'),
            StringComparison.OrdinalIgnoreCase);
    }
}
```

### Pattern 3: Log File Format (UX-01)

**What:** Per-run log files with a JSON summary header followed by timestamped text.

```
=== SERIALIZER SUMMARY ===
{
  "operation": "Deserialize",
  "timestamp": "2026-03-24T14:30:52Z",
  "dryRun": false,
  "predicates": [
    {
      "name": "OrderFlows",
      "table": "EcomOrderFlows",
      "created": 3,
      "updated": 1,
      "skipped": 0,
      "failed": 0,
      "errors": []
    }
  ],
  "totalCreated": 3,
  "totalUpdated": 1,
  "totalSkipped": 0,
  "totalFailed": 0,
  "errors": [],
  "advice": [
    "FK constraint failed on EcomOrderStates -- run OrderFlows predicate first",
    "Missing group: Webshop1 -- create it in Settings > Ecommerce > Shops"
  ]
}
=== END SUMMARY ===
[2026-03-24 14:30:52.001] === Serializer Deserialize started ===
[2026-03-24 14:30:52.010] FK ordering: EcomOrderFlows -> EcomOrderStates
...
```

The viewer screen parses everything between `=== SERIALIZER SUMMARY ===` and `=== END SUMMARY ===` as JSON. The rest is displayed as scrollable text.

### Anti-Patterns to Avoid
- **Using `EditScreenInjector` for `OverviewScreenBase` screens:** Will not compile. Use `ScreenInjector<T>` with `OnAfter()` instead.
- **Hardcoding node IDs without constants:** Use `internal const` fields in the node provider and reference them from path providers -- the existing pattern does this correctly.
- **Loading config in the log viewer screen constructor:** Config loading should happen in the query/model data retrieval, not at DI/construction time.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON serialization for log header | Custom string building | `System.Text.Json.JsonSerializer` | Handles escaping, nested objects correctly |
| Zip extraction | Manual stream handling | `ZipFile.ExtractToDirectory()` | Already used in existing DeserializeCommand |
| Admin tree navigation | Custom URL routing | `NavigationNodeProvider` + `NavigateScreenAction.To<T>()` | DW's built-in navigation framework, auto-discovered |
| Screen action injection | Custom middleware | `ScreenInjector<T>` / `EditScreenInjector<T,M>` | DW AddInManager auto-discovers injectors |
| File path resolution | Hardcoded paths | `ConfigPathResolver` + `SyncConfiguration.EnsureDirectories()` | Already handles cross-platform path normalization |

**Key insight:** DW's AddInManager auto-discovers all classes extending `NavigationNodeProvider`, `ScreenInjector`, `EditScreenInjector`, and `CommandBase`. No manual registration is needed -- just create the class and it will be found at runtime.

## Runtime State Inventory

> This phase includes a rename (REN-01), so runtime state inventory is required.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None -- this project stores YAML files on disk (keyed by content GUID, not namespace). No database tables store the "ContentSync" string as a key or ID. | No data migration needed. |
| Live service config | DW admin tree node IDs (`ContentSync_Settings`, `ContentSync_Predicates`) are registered at runtime by AddInManager scanning the DLL. Old IDs disappear when old DLL is replaced. | Code rename only -- new DLL with new class names auto-registers new node IDs. |
| OS-registered state | None -- scheduled tasks were already deleted (commit `a32703f`). No pm2, systemd, or Task Scheduler entries exist. | None. |
| Secrets/env vars | None -- the project uses no environment variables or secret keys containing "ContentSync". API keys are DW-managed (generic `Authorization: Bearer`). | None. |
| Build artifacts | `bin/` and `obj/` directories contain `Dynamicweb.ContentSync.dll` and related build outputs. NuGet package cache may have old package. Deployed DW instances have `Dynamicweb.ContentSync.dll` in their `bin/` folder. | Clean build required after rename. Old DLL must be manually deleted from DW instance `bin/` before deploying new `DynamicWeb.Serializer.dll`. |

**Additional rename-specific items:**

| Item | Old Value | New Value | Type |
|------|-----------|-----------|------|
| Config file name | `ContentSync.config.json` | `Serializer.config.json` | Code edit (ConfigPathResolver candidate paths) |
| Default output directory | `\System\ContentSync` | `\System\Serializer` | Code edit (ConfigPathResolver default config) |
| Log file path | `ContentSync.log` | Per-run files (D-11) | Code edit + behavior change |
| API endpoints | `/Admin/Api/ContentSyncSerialize`, `/Admin/Api/ContentSyncDeserialize` | `/Admin/Api/SerializerSerialize`, `/Admin/Api/SerializerDeserialize` | Code rename (class name determines endpoint) |
| Solution file | `Dynamicweb.ContentSync.sln` | `DynamicWeb.Serializer.sln` | File rename |
| Project dirs | `src/Dynamicweb.ContentSync/`, `tests/Dynamicweb.ContentSync.Tests/`, `tests/Dynamicweb.ContentSync.IntegrationTests/` | `src/DynamicWeb.Serializer/`, `tests/DynamicWeb.Serializer.Tests/`, `tests/DynamicWeb.Serializer.IntegrationTests/` | Directory rename |
| Csproj files | `Dynamicweb.ContentSync.csproj`, etc. | `DynamicWeb.Serializer.csproj`, etc. | File rename |
| README references | 25 occurrences of "ContentSync" | Updated to "Serializer" / "DynamicWeb.Serializer" | Documentation update |
| User-deployed DLLs | `Dynamicweb.ContentSync.dll` in DW instance `bin/` | Must remove old DLL, deploy `DynamicWeb.Serializer.dll` | Deployment step (README note) |

**The canonical question:** *After every file in the repo is updated, what runtime systems still have the old string cached, stored, or registered?*

Answer: Only the deployed DLL file (`Dynamicweb.ContentSync.dll`) in DW instance `bin/` directories and the existing `ContentSync.config.json` on disk. The README should document that users need to (1) delete the old DLL, and (2) rename their config file from `ContentSync.config.json` to `Serializer.config.json` (or the code should support both names during a transition period).

## Common Pitfalls

### Pitfall 1: EditScreenInjector vs ScreenInjector Type Mismatch
**What goes wrong:** Using `EditScreenInjector<FileOverviewScreen, FileDataModel>` as specified in D-22, which will not compile because `FileOverviewScreen : OverviewScreenBase<FileDataModel>`, not `EditScreenBase<FileDataModel>`.
**Why it happens:** The CONTEXT.md assumed `FileOverviewScreen` was an edit screen. It is actually an overview screen.
**How to avoid:** Use `ScreenInjector<FileOverviewScreen>` with the `OnAfter()` pattern. The `ProductOverviewScreenInjector` in DW ecommerce is the reference pattern.
**Warning signs:** Compiler error: `TScreen must be derived from EditScreenBase<TModel>`.

### Pitfall 2: Incomplete Directory Rename on Windows
**What goes wrong:** Git on Windows sometimes fails to rename directories that differ only in casing (`Dynamicweb.ContentSync` -> `DynamicWeb.Serializer`). The `D` to `D` is the same but `w` to `W` is a case change.
**Why it happens:** Windows filesystem is case-insensitive. Git may not track the case change.
**How to avoid:** Use a two-step rename: first rename to a temporary name, commit, then rename to the final name. Or use `git mv -f` with the exact casing.
**Warning signs:** `git status` shows no changes after a case-only rename.

### Pitfall 3: Stale Old DLL Alongside New DLL
**What goes wrong:** If `Dynamicweb.ContentSync.dll` is not removed from the DW instance `bin/` before deploying `DynamicWeb.Serializer.dll`, DW's AddInManager will discover BOTH and register duplicate tree nodes, commands, and injectors.
**Why it happens:** DW scans all DLLs in `bin/` for add-ins. Both old and new DLLs contain valid add-in types.
**How to avoid:** Document in README that old DLL must be deleted before deploying new one.
**Warning signs:** Duplicate tree nodes in admin, duplicate API command registrations.

### Pitfall 4: Config File Backward Compatibility
**What goes wrong:** After rename, existing installations have `ContentSync.config.json`. If the code only looks for `Serializer.config.json`, existing configs are silently ignored.
**Why it happens:** `ConfigPathResolver` hardcodes the config file name.
**How to avoid:** Update `ConfigPathResolver` to check BOTH `Serializer.config.json` (preferred) and `ContentSync.config.json` (fallback). Log a warning when using the old name.
**Warning signs:** "No configuration found" errors on existing installations after upgrade.

### Pitfall 5: Log File Permissions in Shared Hosting
**What goes wrong:** Per-run log files could accumulate in `Files/System/Serializer/Log/` with no cleanup.
**Why it happens:** D-16 explicitly says no auto-cleanup.
**How to avoid:** This is by design. Document in README that log files accumulate and can be manually deleted.
**Warning signs:** N/A -- this is accepted behavior per user decision.

## Code Examples

### Log File Writing (refactored from single file to per-run)

```csharp
// Current pattern in ContentSyncSerializeCommand:
_logFile = Path.Combine(paths.Log, "ContentSync.log");
File.AppendAllText(_logFile, message);

// New pattern: per-run log file with JSON summary header
public static class LogFileWriter
{
    public static string CreateLogFile(string logDir, string operation)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        var fileName = $"{operation}_{timestamp}.log";
        return Path.Combine(logDir, fileName);
    }

    public static void WriteSummaryHeader(string logFile, LogFileSummary summary)
    {
        var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
        var header = $"=== SERIALIZER SUMMARY ===\n{json}\n=== END SUMMARY ===\n";
        File.WriteAllText(logFile, header);
    }

    public static void AppendLogLine(string logFile, string message)
    {
        File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n");
    }
}
```

### Log File Summary Model

```csharp
public record LogFileSummary
{
    public string Operation { get; init; } = "";
    public DateTime Timestamp { get; init; }
    public bool DryRun { get; init; }
    public List<PredicateSummary> Predicates { get; init; } = new();
    public int TotalCreated { get; init; }
    public int TotalUpdated { get; init; }
    public int TotalSkipped { get; init; }
    public int TotalFailed { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Advice { get; init; } = new();
}

public record PredicateSummary
{
    public string Name { get; init; } = "";
    public string Table { get; init; } = "";
    public int Created { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public List<string> Errors { get; init; } = new();
}
```

### Advice Rule Generation

```csharp
public static class AdviceGenerator
{
    public static List<string> GenerateAdvice(OrchestratorResult result)
    {
        var advice = new List<string>();

        foreach (var dr in result.DeserializeResults.Where(r => r.HasErrors))
        {
            foreach (var error in dr.Errors)
            {
                if (error.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
                    advice.Add($"FK constraint failed on {dr.TableName} -- check that parent tables are deserialized first (verify predicate ordering)");
                else if (error.Contains("group", StringComparison.OrdinalIgnoreCase) && error.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    advice.Add($"Missing group referenced in {dr.TableName} -- create it in Settings > Ecommerce before re-running");
                else if (error.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
                    advice.Add($"Duplicate key in {dr.TableName} -- check NameColumn uniqueness in source data");
                else
                    advice.Add($"Error in {dr.TableName}: {error}");
            }
        }

        if (result.DeserializeResults.Any(r => r.Failed > 0))
            advice.Add("Re-run deserialization after fixing errors -- successfully applied rows will be skipped (source-wins idempotency)");

        return advice.Distinct().ToList();
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Single `ContentSync.log` file (append) | Per-run log files with JSON header | This phase | Log viewer can parse structured data, each run is isolated |
| Tree under Content > Sync | Tree under Database > Serialize | This phase | Better organizational fit -- serialization is database-level, not content-specific |
| `Dynamicweb.ContentSync` namespace | `DynamicWeb.Serializer` namespace | This phase | Reflects expanded scope beyond just content |

**Deprecated/outdated:**
- Scheduled tasks: Already removed (commit `a32703f`). README will document API commands as replacement.
- Single log file: Replaced by per-run files for UX-01 log viewer.

## Open Questions

1. **Config file backward compatibility strategy**
   - What we know: `ConfigPathResolver` hardcodes `ContentSync.config.json`. Users have existing config files.
   - What's unclear: Should the code support both old and new names permanently, or only during a transition period?
   - Recommendation: Check both `Serializer.config.json` (preferred) and `ContentSync.config.json` (fallback) indefinitely. Log a deprecation warning when using the old name. This is simple to implement and avoids breaking existing installations.

2. **Output directory path in config files**
   - What we know: Default config creates `\System\ContentSync` as the output directory. This is a user-configured value stored in config JSON.
   - What's unclear: Should existing configs with `\System\ContentSync` be auto-migrated to `\System\Serializer`?
   - Recommendation: Do NOT auto-migrate the output directory path -- it is a user-configured value and may have been customized. The old path will continue to work. Only change the DEFAULT for new installations.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 + Moq 4.20.72 |
| Config file | `tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj` (post-rename) |
| Quick run command | `dotnet test tests/DynamicWeb.Serializer.Tests/ --filter "Category!=Integration" -x` |
| Full suite command | `dotnet test` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| REN-01 | All namespaces, assembly name, csproj updated | build | `dotnet build` (compilation = passing test) | N/A |
| REN-01 | Existing unit tests pass under new namespace | unit | `dotnet test tests/DynamicWeb.Serializer.Tests/` | Existing (renamed) |
| UX-01 | LogFileSummary JSON serialization round-trip | unit | `dotnet test --filter LogFileSummary` | Wave 0 |
| UX-01 | AdviceGenerator produces correct suggestions from error patterns | unit | `dotnet test --filter AdviceGenerator` | Wave 0 |
| UX-01 | LogFileWriter creates per-run files with correct naming | unit | `dotnet test --filter LogFileWriter` | Wave 0 |
| UX-02 | SerializerFileOverviewInjector gates on .zip extension | unit | `dotnet test --filter FileOverviewInjector` | Wave 0 |
| UX-02 | SerializerFileOverviewInjector gates on output directory | unit | `dotnet test --filter FileOverviewInjector` | Wave 0 |
| UX-03 | Tree node parent changed to Settings_Database | manual-only | Visual verification in DW admin | N/A -- requires running DW instance |
| UX-04 | README documents API commands as replacement | manual-only | Review README content | N/A |

### Sampling Rate
- **Per task commit:** `dotnet build && dotnet test tests/DynamicWeb.Serializer.Tests/ --filter "Category!=Integration" -x`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/DynamicWeb.Serializer.Tests/AdminUI/LogFileWriterTests.cs` -- covers UX-01 log file creation and naming
- [ ] `tests/DynamicWeb.Serializer.Tests/AdminUI/LogFileSummaryTests.cs` -- covers UX-01 JSON round-trip
- [ ] `tests/DynamicWeb.Serializer.Tests/AdminUI/AdviceGeneratorTests.cs` -- covers UX-01 advice rules
- [ ] `tests/DynamicWeb.Serializer.Tests/AdminUI/FileOverviewInjectorTests.cs` -- covers UX-02 gating logic (extension + path)

*(Note: Wave 0 tests will be created as part of Wave 2, after the rename in Wave 1 establishes the new namespace)*

## Sources

### Primary (HIGH confidence)
- DW10 source: `Dynamicweb.Application.UI/SystemNodeProvider.cs` -- `Settings_Database` node ID = `"Settings_" + "Database"` = `"Settings_Database"`, confirmed from source code line 82 + PREFIX on line 55
- DW10 source: `Dynamicweb.CoreUI/Screens/EditScreenInjector.cs` -- constraint `where TScreen : EditScreenBase<TModel>` confirms incompatibility with OverviewScreenBase
- DW10 source: `Dynamicweb.CoreUI/Screens/OverviewScreenBase.cs` -- `FileOverviewScreen` base class, has `GetScreenActions()` and `ContextActionGroups` on `ScreenLayout`
- DW10 source: `Dynamicweb.Ecommerce.UI/Injectors/ProductOverviewScreenInjector.cs` -- reference pattern for `ScreenInjector<OverviewScreen>` with `OnAfter()` action injection
- DW10 source: `Dynamicweb.Files.UI/Screens/Files/FileOverviewScreen.cs` -- target screen for UX-02 injector
- DW10 source: `Dynamicweb.Files.UI/Models/FileDataModel.cs` -- `Extension`, `FilePath`, `DirectoryPath` properties confirmed
- NuGet API: `Dynamicweb.Files.UI` version 10.23.9 confirmed available
- Project source: 68 .cs files in src/, 130 namespace occurrences, 96 test occurrences needing rename

### Secondary (MEDIUM confidence)
- DW10 source: `Dynamicweb.Application.UI/AreasSection.cs` and `SystemSection.cs` -- both are `NavigationSection<SettingsArea>`, confirming the section type change is safe

### Tertiary (LOW confidence)
- None -- all findings verified from source code

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - all packages already in use or confirmed as transitive dependencies
- Architecture: HIGH - all patterns verified against DW10 source code, critical type mismatch (EditScreenInjector vs ScreenInjector) caught
- Pitfalls: HIGH - derived from source code analysis, not speculation
- Rename scope: HIGH - exact file counts from grep, all rename targets enumerated

**Research date:** 2026-03-24
**Valid until:** 2026-04-24 (stable -- DW 10.23.9 is a pinned version)
