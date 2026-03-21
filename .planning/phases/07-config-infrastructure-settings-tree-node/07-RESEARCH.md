# Phase 7: Config Infrastructure + Settings Tree Node - Research

**Researched:** 2026-03-21
**Domain:** DW10 admin UI navigation tree + config file read/write
**Confidence:** HIGH

## Summary

This phase has two independent workstreams: (1) a ConfigWriter companion to ConfigLoader for atomic JSON file writes, and (2) registering a "Sync" navigation node under Settings > Content in the DW admin tree. Both are well-understood from source code analysis.

The navigation tree architecture is fully understood from DW10 source. The key finding is that `NavigationNodeProvider<AreasSection>` is the correct base type for adding nodes under Settings > Areas. The existing `Dynamicweb.Content.UI.SettingsNodeProvider` already creates a "Content" root node (Id: `Content_Settings`) with `HasSubNodes = true`. Our plugin can inject a "Sync" sub-node by creating its own `NavigationNodeProvider<AreasSection>` that returns the Sync node from `GetSubNodes()` when the parent path ends with `Content_Settings`. DW's `NavigationSectionProvider` queries ALL registered providers of the same section type for both root nodes and sub-nodes.

The config writer is straightforward: serialize `SyncConfiguration` back to JSON using System.Text.Json (matching ConfigLoader's existing deserializer) and write atomically via temp-file-then-rename. The project SDK must change from `Microsoft.NET.Sdk` to `Microsoft.NET.Sdk.Razor` to support future UI views, and new NuGet references are needed for the CoreUI framework.

**Primary recommendation:** Use `NavigationNodeProvider<AreasSection>` with empty `GetRootNodes()` and return "Sync" sub-node when parent is `Content_Settings`. Use `Dynamicweb.Content.UI` 10.23.9 as the single NuGet addition for all UI infrastructure.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Add a ConfigWriter companion to ConfigLoader -- writes SyncConfiguration back to JSON with same format/casing as the original
- **D-02:** Simple file-level read/write -- no ReaderWriterLockSlim or heavy concurrency machinery. This is not a high-contention scenario.
- **D-03:** Atomic write via temp-file-then-rename to prevent corruption on crash mid-write
- **D-04:** ConfigLoader already validates on read -- writer should produce valid JSON that passes the same validation
- **D-05:** UI uses the same FindConfigFile() discovery logic as scheduled tasks -- single source of truth for config path
- **D-06:** If no config file exists yet, create at the first candidate path (BaseDirectory/ContentSync.config.json) with sensible defaults
- **D-07:** Sync node appears under Settings > Content using NavigationNodeProvider pattern from ExpressDelivery sample
- **D-08:** Clicking Sync node navigates directly to the settings edit screen (Phase 8 populates the actual fields; this phase provides the skeleton screen)
- **D-09:** Predicates sub-node registered under Sync with HasSubNodes=true on the parent (Phase 9 builds the predicate screens)
- **D-10:** Settings screen always reads fresh from config file on load -- no caching. Manual edits reflected immediately on next screen open.

### Claude's Discretion
- Exact JSON serialization options (indentation, property naming)
- Whether to use System.Text.Json or Newtonsoft for writing (ConfigLoader already uses System.Text.Json)
- NavigationNodePathProvider breadcrumb implementation
- Placeholder screen design before Phase 8 fills it in

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| CFG-01 | Config file read/write is concurrency-safe (file locking prevents corruption from simultaneous UI and scheduled task access) | Atomic write via temp-file-then-rename (D-03). Simple approach per D-02 -- no ReaderWriterLockSlim. File.Move with overwrite is atomic on NTFS/modern filesystems. |
| CFG-02 | Admin UI reflects manual config file edits on next screen load (bidirectional sync) | Always read fresh from disk on screen load (D-10). No caching. Query reads config file each time. |
| CFG-03 | Config file validation produces clear error messages for invalid values | ConfigLoader.Load() already validates with clear messages. Writer produces valid JSON. Screen should catch and surface validation errors from ConfigLoader. |
| UI-01 | Sync node appears at Settings > Content > Sync in DW admin navigation tree | NavigationNodeProvider<AreasSection> injects "Sync" sub-node under existing "Content" root (Content_Settings). Verified from DW10 source. |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Dynamicweb.Content.UI | 10.23.9 | UI framework (CoreUI, Application.UI, navigation, screens, commands) | Transitively includes Dynamicweb.Application.UI (AreasSection, SettingsArea) and Dynamicweb.CoreUI (navigation, screens, commands). Matches existing Dynamicweb 10.23.9. |
| Dynamicweb.CoreUI.Rendering | 10.23.9 | IRenderingBundle marker interface for assembly scanning | Required for DW to discover embedded static resources. Both ExpressDelivery and AIDiagnoser reference this. |
| System.Text.Json | (built-in) | JSON serialization/deserialization | ConfigLoader already uses it. No reason to add Newtonsoft. |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.Extensions.FileProviders.Embedded | 8.0.* | Embedded file provider for wwwroot resources | Required when project uses Razor SDK with embedded resources |
| Microsoft.AspNetCore.Components.Web | 8.0.* | Razor component support | May be needed for Razor SDK; ExpressDelivery includes it |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| System.Text.Json | Newtonsoft.Json | Unnecessary dependency -- STJ is already in use and sufficient |
| Dynamicweb.Content.UI | Dynamicweb.Suite.Ring1 | Ring1 pulls everything; Content.UI is more targeted. Ring1 version (2026.3.18) may not match 10.23.9 numbering. |
| Dynamicweb.Content.UI | Dynamicweb.CoreUI + Dynamicweb.Application.UI separately | Content.UI brings both as transitive deps plus Content-specific types we may need later |

**Installation:**
```bash
dotnet add src/Dynamicweb.ContentSync/Dynamicweb.ContentSync.csproj package Dynamicweb.Content.UI --version 10.23.9
dotnet add src/Dynamicweb.ContentSync/Dynamicweb.ContentSync.csproj package Dynamicweb.CoreUI.Rendering --version 10.23.9
dotnet add src/Dynamicweb.ContentSync/Dynamicweb.ContentSync.csproj package Microsoft.Extensions.FileProviders.Embedded --version 8.0.8
```

**Version verification:** All three packages verified on NuGet at version 10.23.9 (DW packages) and 8.0.8 (Microsoft) on 2026-03-21.

## Architecture Patterns

### Recommended Project Structure
```
src/Dynamicweb.ContentSync/
├── Configuration/
│   ├── ConfigLoader.cs         # Existing: read + validate
│   ├── ConfigWriter.cs         # NEW: atomic write
│   ├── ConfigPathResolver.cs   # NEW: extracted FindConfigFile() + default creation
│   ├── SyncConfiguration.cs    # Existing: config record
│   └── PredicateDefinition.cs  # Existing: predicate record
├── AdminUI/
│   ├── Tree/
│   │   ├── SyncSettingsNodeProvider.cs       # NavigationNodeProvider<AreasSection>
│   │   └── SyncNavigationNodePathProvider.cs # Breadcrumb path provider
│   ├── Screens/
│   │   └── SyncSettingsEditScreen.cs         # EditScreenBase<SyncSettingsModel> skeleton
│   ├── Models/
│   │   └── SyncSettingsModel.cs              # DataViewModelBase for edit screen
│   ├── Queries/
│   │   └── SyncSettingsQuery.cs              # DataQueryModelBase — reads config file
│   └── Commands/
│       └── SaveSyncSettingsCommand.cs        # CommandBase — writes config file
├── Infrastructure/
│   └── RenderingBundle.cs      # NEW: IRenderingBundle marker
└── Dynamicweb.ContentSync.csproj  # Updated: Razor SDK + new NuGet refs
```

### Pattern 1: NavigationNodeProvider for Sub-Node Injection
**What:** Adding a node under an existing parent node owned by another assembly
**When to use:** When you need to place your node under a DW built-in tree node (like "Content" under Settings)
**How it works:** DW's `NavigationSectionProvider.GetNodeProviders()` collects ALL `NavigationNodeProvider` instances matching the section type. When expanding a node, `GetSubNodes()` is called on EVERY provider. Your provider returns nodes only when the parent matches.

```csharp
// Source: DW10 source analysis + ExpressDelivery pattern
using Dynamicweb.Application.UI;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Icons;
using Dynamicweb.CoreUI.Navigation;

public sealed class SyncSettingsNodeProvider : NavigationNodeProvider<AreasSection>
{
    // The Content root node ID from Dynamicweb.Content.UI.SettingsNodeProvider
    private const string ContentRootId = "Content_Settings";
    internal const string SyncNodeId = "ContentSync_Settings";
    internal const string PredicatesNodeId = "ContentSync_Predicates";

    public override IEnumerable<NavigationNode> GetRootNodes()
    {
        // We do NOT create a root node — "Content" already exists
        yield break;
    }

    public override IEnumerable<NavigationNode> GetSubNodes(NavigationNodePath parentNodePath)
    {
        if (parentNodePath.Last == ContentRootId)
        {
            yield return new NavigationNode
            {
                Id = SyncNodeId,
                Name = "Sync",
                Icon = Icon.Sync,
                Sort = 100, // After existing Content sub-nodes
                HasSubNodes = true,  // D-09: Predicates sub-node in Phase 9
                NodeAction = NavigateScreenAction.To<SyncSettingsEditScreen>()
                    .With(new SyncSettingsQuery())
            };
        }
        else if (parentNodePath.Last == SyncNodeId)
        {
            yield return new NavigationNode
            {
                Id = PredicatesNodeId,
                Name = "Predicates",
                Icon = Icon.Filter,
                Sort = 10,
                HasSubNodes = false
                // Phase 9 will add NodeAction here
            };
        }
    }
}
```

### Pattern 2: NavigationNodePathProvider for Breadcrumbs
**What:** Maps a data model to its navigation tree path for breadcrumb rendering
**When to use:** Required for any screen that should show breadcrumbs in the DW admin

```csharp
// Source: ExpressDelivery sample + DW10 source
using Dynamicweb.Application.UI;
using Dynamicweb.CoreUI.Navigation;

public sealed class SyncNavigationNodePathProvider : NavigationNodePathProvider<SyncSettingsModel>
{
    public SyncNavigationNodePathProvider()
    {
        AllowNullModel = true;
    }

    protected override NavigationNodePath GetNavigationNodePathInternal(SyncSettingsModel? model) =>
        new([
            typeof(SettingsArea).FullName,       // "Settings"
            NavigationContext.Empty,             // No context value needed
            typeof(AreasSection).FullName,       // "Areas" section
            "Content_Settings",                  // Content root node
            SyncSettingsNodeProvider.SyncNodeId   // Our Sync node
        ]);
}
```

### Pattern 3: EditScreenBase with Config-File-Backed Model
**What:** An edit screen whose data comes from a JSON config file instead of a database
**When to use:** When the model is persisted to disk, not to DW's SQL database
**Key difference from SettingsViewModelBase:** Do NOT use `SettingsViewModelBase` (that's for DW GlobalSettings). Use plain `DataViewModelBase` with a custom query that reads the config file.

```csharp
// DataModel — must end with "Model" (enforced by DataViewModelBase)
public sealed class SyncSettingsModel : DataViewModelBase
{
    [ConfigurableProperty("Output Directory", explanation: "Root path for serialized YAML files")]
    public string OutputDirectory { get; set; } = string.Empty;

    [ConfigurableProperty("Log Level")]
    public string LogLevel { get; set; } = "info";

    // Phase 8 will add more fields
}

// Query — reads config file fresh each time (CFG-02)
public sealed class SyncSettingsQuery : DataQueryModelBase<SyncSettingsModel>
{
    public override SyncSettingsModel? GetModel()
    {
        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null)
            return new SyncSettingsModel(); // Defaults for new config

        var config = ConfigLoader.Load(configPath);
        return new SyncSettingsModel
        {
            OutputDirectory = config.OutputDirectory,
            LogLevel = config.LogLevel
        };
    }
}

// EditScreen skeleton — Phase 8 fills in BuildEditScreen
public sealed class SyncSettingsEditScreen : EditScreenBase<SyncSettingsModel>
{
    protected override void BuildEditScreen()
    {
        // Phase 8 will add EditorFor() calls here
        // For now, minimal placeholder
        AddComponents("Settings", "Content Sync", [
            EditorFor(m => m.OutputDirectory),
            EditorFor(m => m.LogLevel)
        ]);
    }

    protected override string GetScreenName() => "Content Sync Settings";
    protected override CommandBase<SyncSettingsModel> GetSaveCommand() => new SaveSyncSettingsCommand();
}
```

### Pattern 4: Atomic Config File Write
**What:** Write JSON config to a temp file then rename to prevent corruption
**When to use:** Any file write that could be interrupted by crash or concurrent read

```csharp
// ConfigWriter — companion to ConfigLoader
public static class ConfigWriter
{
    private static readonly JsonSerializerOptions _writeOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Save(SyncConfiguration config, string filePath)
    {
        var json = JsonSerializer.Serialize(config, _writeOptions);

        // Atomic write: temp file + rename (D-03)
        var tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, filePath, overwrite: true);
    }
}
```

### Pattern 5: ConfigPathResolver (Extracted FindConfigFile)
**What:** Centralized config file path resolution reusable by both scheduled tasks and UI
**When to use:** Any code that needs to locate ContentSync.config.json

```csharp
public static class ConfigPathResolver
{
    private static readonly string[] CandidatePaths =
    [
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "wwwroot", "Files", "ContentSync.config.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Files", "ContentSync.config.json"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ContentSync.config.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "ContentSync.config.json")
    ];

    // D-06: Default creation path
    public static string DefaultPath => CandidatePaths[0];

    public static string? FindConfigFile()
    {
        foreach (var path in CandidatePaths)
        {
            if (File.Exists(path))
                return Path.GetFullPath(path);
        }
        return null;
    }

    public static string FindOrCreateConfigFile()
    {
        var existing = FindConfigFile();
        if (existing != null) return existing;

        // Create default config at first candidate path
        var defaultPath = Path.GetFullPath(DefaultPath);
        var dir = Path.GetDirectoryName(defaultPath);
        if (dir != null) Directory.CreateDirectory(dir);

        var defaultConfig = new SyncConfiguration
        {
            OutputDirectory = "./ContentSync",
            LogLevel = "info",
            Predicates = [new PredicateDefinition { Name = "Default", Path = "/", AreaId = 1 }]
        };
        ConfigWriter.Save(defaultConfig, defaultPath);
        return defaultPath;
    }
}
```

### Anti-Patterns to Avoid
- **Using SettingsViewModelBase for config-file-backed settings:** SettingsViewModelBase auto-loads from DW GlobalSettings via `SettingsService.Load(this)`. Our config lives in a JSON file, not GlobalSettings. Use plain `DataViewModelBase`.
- **Creating a new root node under AreasSection:** The "Content" root node already exists (created by `Dynamicweb.Content.UI.SettingsNodeProvider`). Creating another root would put "Sync" at the same level as "Content", not under it.
- **Caching config reads:** Per D-10, always read fresh from disk. This ensures manual edits are reflected immediately.
- **Using ReaderWriterLockSlim:** Per D-02, this is not a high-contention scenario. Atomic write via temp+rename is sufficient.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Navigation tree node | Custom HTML/JS tree injection | NavigationNodeProvider<AreasSection> | DW discovers providers via assembly scanning; custom approaches break on updates |
| Edit screen form rendering | Custom Razor views for form fields | EditScreenBase<T> + ConfigurableProperty attributes | CoreUI renders form fields automatically from model properties |
| Breadcrumb path resolution | Manual URL/path construction | NavigationNodePathProvider<T> | DW's built-in breadcrumb system handles this |
| JSON serialization | Manual string building | System.Text.Json JsonSerializer | Already used by ConfigLoader; maintains format consistency |
| Config file discovery | Hardcoded paths | ConfigPathResolver (extracted from FindConfigFile) | Must match scheduled task discovery logic exactly |

**Key insight:** DW10 CoreUI handles all UI rendering through convention-based assembly scanning. If you follow the patterns (NavigationNodeProvider, EditScreenBase, DataViewModelBase, CommandBase), DW renders everything automatically. Custom HTML/Razor is only needed for truly novel UI elements.

## Common Pitfalls

### Pitfall 1: Wrong SectionType Placing Node at Wrong Level
**What goes wrong:** Using a different section type (e.g., `NavigationNodeProvider<SystemSection>`) places the node under "System" instead of under Content in "Areas"
**Why it happens:** Each NavigationSection is a separate branch of the tree. AreasSection contains Content, Products, Users, etc.
**How to avoid:** Always use `NavigationNodeProvider<AreasSection>` and inject via `GetSubNodes` responding to the `Content_Settings` parent ID
**Warning signs:** Node appears at wrong location in the admin tree

### Pitfall 2: DataViewModelBase Class Name Must End with "Model"
**What goes wrong:** `DataViewModelBase` constructor throws `Exception` if class name doesn't end with "Model"
**Why it happens:** DW uses `AddInManager.SanitizeTypeName()` and checks the suffix as a convention
**How to avoid:** Always name data model classes with the "Model" suffix (e.g., `SyncSettingsModel`)
**Warning signs:** Runtime exception on first screen load: "Implementations inheriting DataViewModelBase has to be called '{NameOfModel}Model'"

### Pitfall 3: Project SDK Must Be Microsoft.NET.Sdk.Razor
**What goes wrong:** Embedded wwwroot resources aren't discovered; IRenderingBundle scanning fails
**Why it happens:** The standard .NET SDK doesn't process Razor views or embedded file manifests
**How to avoid:** Change project SDK from `Microsoft.NET.Sdk` to `Microsoft.NET.Sdk.Razor` and add `<AddRazorSupportForMvc>true</AddRazorSupportForMvc>` and `<GenerateEmbeddedFilesManifest>true</GenerateEmbeddedFilesManifest>`
**Warning signs:** UI screens don't render; "view not found" errors

### Pitfall 4: File.Move Atomicity on Windows
**What goes wrong:** `File.Move(temp, target, overwrite: true)` may not be truly atomic on all Windows file systems
**Why it happens:** NTFS rename-with-overwrite is atomic, but FAT32 or network shares may not be
**How to avoid:** For this project, NTFS is the deployment target. `File.Move` with `overwrite: true` is the standard approach. Log any IOException and retry once.
**Warning signs:** Corrupted config file after crash during write

### Pitfall 5: Content_Settings Node ID is Internal Knowledge
**What goes wrong:** If `Dynamicweb.Content.UI` changes the `Content_Settings` ID in a future version, our sub-node injection breaks
**Why it happens:** The ID is defined as `internal static string RootId => $"{PREFIX}Settings"` in Content.UI's SettingsNodeProvider
**How to avoid:** Use the string constant `"Content_Settings"` and add a comment explaining the dependency. Add an integration test that verifies the node appears correctly.
**Warning signs:** Sync node disappears after DW upgrade

### Pitfall 6: SyncConfiguration Records Are Init-Only
**What goes wrong:** Can't modify `SyncConfiguration` properties after construction because they use `init` setters
**Why it happens:** The existing model uses C# records with `required` and `init` properties
**How to avoid:** ConfigWriter accepts a fully constructed SyncConfiguration. The SaveCommand constructs a new record from the form model. No need to mutate existing records.
**Warning signs:** Compiler errors trying to set `init`-only properties

## Code Examples

### Project File Changes (csproj)
```xml
<!-- Change SDK from Microsoft.NET.Sdk to Microsoft.NET.Sdk.Razor -->
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Dynamicweb.ContentSync</RootNamespace>
    <AssemblyName>Dynamicweb.ContentSync</AssemblyName>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
    <GenerateEmbeddedFilesManifest>true</GenerateEmbeddedFilesManifest>
    <!-- ...existing NuGet metadata... -->
  </PropertyGroup>

  <ItemGroup>
    <EmbeddedResource Include="wwwroot/**/*" />
  </ItemGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Dynamicweb" Version="10.23.9" />
    <PackageReference Include="Dynamicweb.Content.UI" Version="10.23.9" />
    <PackageReference Include="Dynamicweb.CoreUI.Rendering" Version="10.23.9" />
    <PackageReference Include="YamlDotNet" Version="13.7.1" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.1" />
    <PackageReference Include="Microsoft.Extensions.FileProviders.Embedded" Version="8.0.8" />
  </ItemGroup>

</Project>
```

### IRenderingBundle Marker
```csharp
// Source: AIDiagnoser + ExpressDelivery pattern
using Dynamicweb.CoreUI.Rendering;

namespace Dynamicweb.ContentSync.Infrastructure;

public class RenderingBundle : IRenderingBundle { }
```

### JSON Write Options (Claude's Discretion Resolution)
```csharp
// Use camelCase to match existing config format, indented for readability
private static readonly JsonSerializerOptions _writeOptions = new()
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};
```
**Rationale:** ConfigLoader uses `PropertyNameCaseInsensitive = true` for reading, meaning it accepts any casing. The existing config files use camelCase (e.g., `outputDirectory`, `logLevel`, `predicates`). Writer should match this convention. Indented output makes manual editing easier, supporting CFG-02.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Custom tree providers per assembly | NavigationNodeProvider<T> with section type matching | DW10 | All providers for same section type are merged automatically |
| ConfigurableAddIn for settings | SettingsViewModelBase + Settings attribute | DW10 | Settings stored in GlobalSettings DB, not config files (NOT our pattern) |
| File.Replace for atomic writes | File.Move with overwrite: true (.NET 6+) | .NET 6 | Simpler API, same NTFS atomicity guarantee |

**Deprecated/outdated:**
- `Dynamicweb.Core.UI` (version 3.0.1): Old DW9 UI framework. Do not confuse with `Dynamicweb.CoreUI` (10.23.9).

## Open Questions

1. **Content_Settings node ID stability**
   - What we know: The ID is `"Content_Settings"` constructed from `$"{PREFIX}Settings"` where PREFIX = `"Content_"` in Dynamicweb.Content.UI.SettingsNodeProvider
   - What's unclear: Whether this ID is considered a stable contract across DW10 versions
   - Recommendation: Use the literal string with a comment. Runtime verification during integration testing will catch any changes.

2. **Sort order for Sync node under Content**
   - What we know: Existing Content sub-nodes use sorts 10-90 (Styles=10, Item types=30, Grid=40, Interface=50, Languages=60, Richtext=80, App settings=90)
   - What's unclear: Whether new sub-nodes at sort=100 appear at the bottom as expected
   - Recommendation: Use Sort=100. Easy to adjust if placement isn't right.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 + Moq 4.20.72 |
| Config file | tests/Dynamicweb.ContentSync.Tests/Dynamicweb.ContentSync.Tests.csproj |
| Quick run command | `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "FullyQualifiedName~Configuration" --no-build -q` |
| Full suite command | `dotnet test tests/Dynamicweb.ContentSync.Tests --no-build -q` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CFG-01 | Atomic write via temp+rename; concurrent read doesn't see partial data | unit | `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "FullyQualifiedName~ConfigWriter" -x` | Wave 0 |
| CFG-02 | Fresh read on every query (no caching) | unit | `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "FullyQualifiedName~ConfigWriter" -x` | Wave 0 |
| CFG-03 | Validation errors are clear messages with field names | unit | `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "FullyQualifiedName~ConfigLoader" -x` | Existing |
| UI-01 | Sync node appears under Content_Settings parent | manual-only | N/A -- requires running DW instance | N/A |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "FullyQualifiedName~Config" --no-build -q`
- **Per wave merge:** `dotnet test tests/Dynamicweb.ContentSync.Tests --no-build -q`
- **Phase gate:** Full suite green + visual verification of tree node in DW admin

### Wave 0 Gaps
- [ ] `tests/Dynamicweb.ContentSync.Tests/Configuration/ConfigWriterTests.cs` -- covers CFG-01, CFG-02 (write + round-trip)
- [ ] `tests/Dynamicweb.ContentSync.Tests/Configuration/ConfigPathResolverTests.cs` -- covers config path discovery and default creation

*(UI-01 is manual-only: requires running DW instance to verify tree node appears)*

## Sources

### Primary (HIGH confidence)
- DW10 Source: `C:\Projects\temp\dw10source\Dynamicweb.Application.UI\AreasSection.cs` -- AreasSection is NavigationSection<SettingsArea>
- DW10 Source: `C:\Projects\temp\dw10source\Dynamicweb.Content.UI\SettingsNodeProvider.cs` -- Content root node ID is "Content_Settings", sub-node injection pattern
- DW10 Source: `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Navigation\NavigationSectionProvider.cs` -- GetNodeProviders queries ALL providers of matching section type
- DW10 Source: `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Navigation\NavigationNodeProvider.cs` -- Base class architecture
- DW10 Source: `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Data\DataViewModelBase.cs` -- Model name must end with "Model"
- DW10 Source: `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Data\SettingsViewModelBase.cs` -- Auto-loads from GlobalSettings (NOT for config files)
- ExpressDelivery Sample: `C:\Projects\temp\dwextensionsample\Samples-main\ExpressDelivery\` -- NavigationNodeProvider, EditScreenBase, CommandBase patterns
- AIDiagnoser: `C:\VibeCode\DynamicWeb.AIDiagnoser\` -- Real-world DW admin UI integration, RenderingBundle, csproj references

### Secondary (MEDIUM confidence)
- NuGet registry search: Dynamicweb.Content.UI 10.23.9, Dynamicweb.CoreUI.Rendering 10.23.9 -- verified available

### Tertiary (LOW confidence)
- File.Move atomicity on NTFS -- widely reported as atomic but not formally documented by Microsoft for the overwrite case

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- verified from NuGet, DW10 source, two reference projects
- Architecture: HIGH -- verified from DW10 source code (navigation provider resolution, section provider merging)
- Pitfalls: HIGH -- identified from source code analysis (DataViewModelBase name check, SettingsViewModelBase trap)
- Config writer: HIGH -- straightforward System.Text.Json + File.Move pattern

**Research date:** 2026-03-21
**Valid until:** 2026-04-21 (stable DW10 APIs, unlikely to change within 30 days)
