# Technology Stack

**Project:** DynamicWeb.Serializer v2.0
**Researched:** 2026-03-23

## Current Stack (Already In Place -- DO NOT Re-add)

| Technology | Version | Purpose |
|------------|---------|---------|
| .NET | 8.0 | Target framework |
| Dynamicweb | 10.23.9 | Core CMS NuGet (includes Dynamicweb.Core, Dynamicweb.Configuration) |
| Dynamicweb.Content.UI | 10.23.9 | Content admin UI extensions |
| Dynamicweb.CoreUI.Rendering | 10.23.9 | CoreUI screen/command framework |
| YamlDotNet | 13.7.1 | YAML serialization/deserialization |
| Microsoft.Extensions.Configuration.Json | 8.0.1 | Config file loading |
| Microsoft.Extensions.FileProviders.Embedded | 8.0.15 | Embedded wwwroot resources |
| Microsoft.AspNetCore.App | (framework ref) | ASP.NET Core runtime |

## Stack Additions for v2.0

### No New NuGet Packages Needed

The existing `Dynamicweb` 10.23.9 NuGet already bundles everything required for v2.0. The key APIs are already available through transitive dependencies:

| API | Namespace | Assembly | Purpose | Confidence |
|-----|-----------|----------|---------|------------|
| `Database.CreateDataReader()` | `Dynamicweb.Data` | Dynamicweb.Core.dll | Read SQL table rows | HIGH |
| `Database.ExecuteNonQuery()` | `Dynamicweb.Data` | Dynamicweb.Core.dll | Insert/update/delete SQL rows | HIGH |
| `Database.ExecuteScalar()` | `Dynamicweb.Data` | Dynamicweb.Core.dll | Single-value SQL queries | HIGH |
| `CommandBuilder` | `Dynamicweb.Data` | Dynamicweb.Core.dll | Parameterized SQL (anti-injection) | HIGH |
| `SystemConfiguration.Instance` | `Dynamicweb.Configuration` | Dynamicweb.Configuration.dll | Read/write GlobalSettings | HIGH |
| `EditScreenBase<T>` | `Dynamicweb.CoreUI.Screens` | Dynamicweb.CoreUI.dll | Admin UI edit screens | HIGH |
| `ListScreenBase<T>` | `Dynamicweb.CoreUI.Screens` | Dynamicweb.CoreUI.dll | Admin UI list screens (log viewer) | HIGH |
| `CommandBase<T>` / `CommandBase` | `Dynamicweb.CoreUI.Data` | Dynamicweb.CoreUI.dll | Admin UI commands | HIGH |
| `DataQueryModelBase<T>` | `Dynamicweb.CoreUI.Data` | Dynamicweb.CoreUI.dll | Admin UI data queries | HIGH |
| `NavigationNodeProvider<T>` | `Dynamicweb.CoreUI` | Dynamicweb.CoreUI.dll | Admin tree node registration | HIGH |

**Rationale:** DynamicWeb 10 follows a "batteries included" model -- the `Dynamicweb` NuGet meta-package brings in `Dynamicweb.Core`, `Dynamicweb.Configuration`, and all other sub-assemblies. Adding Dapper, EF Core, or any external ORM would be wrong -- DW10 apps use `CommandBuilder` + `Database` for SQL access. This is the canonical pattern shown in official DW10 training materials and the AppStore app guide.

### Why NOT Add External Libraries

| Library | Why Not |
|---------|---------|
| Dapper / EF Core | DW10 has its own `Database` + `CommandBuilder` pattern. Adding an ORM would conflict with DW's connection management and not participate in DW's built-in performance logging. |
| System.Data.SqlClient | Already a transitive dependency of Dynamicweb.Core. Do not add directly. |
| Newtonsoft.Json / System.Text.Json | DataGroup XML files are parsed with `System.Xml.Linq` (built-in). Settings are read via `SystemConfiguration.Instance`. No JSON parsing needed beyond what's already in the project. |
| Any logging framework | DW10 has built-in logging. The v1.x `Action<string>` log callback pattern works well and writes to flat files. For the log viewer, we read those files -- no structured logging library needed. |
| Any DI container | DW10 uses `Services.Xxx` static accessors and its own service resolution. Provider instances are created by our own factory, not a container. |

## Core APIs for Each Provider Type

### SqlTableProvider -- `Dynamicweb.Data` Namespace

The workhorse for ~74 data groups. Uses DW10's canonical database access pattern.

**Serialize (read all rows):**
```csharp
using Dynamicweb.Data;

var cmd = new CommandBuilder();
cmd.Add($"SELECT * FROM [{tableName}]");
using var reader = Database.CreateDataReader(cmd);
while (reader.Read())
{
    // Read each column by name, build Dictionary<string, object>
    for (int i = 0; i < reader.FieldCount; i++)
    {
        var name = reader.GetName(i);
        var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
        row[name] = value;
    }
}
```

**Deserialize (upsert by NameColumn or PK):**
```csharp
// Match by NameColumn (from DataGroup XML config)
var matchCmd = CommandBuilder.Create(
    $"SELECT COUNT(*) FROM [{tableName}] WHERE [{nameColumn}] = {{0}}", nameValue);
var exists = Convert.ToInt32(Database.ExecuteScalar(matchCmd)) > 0;

if (exists)
{
    // UPDATE with parameterized CommandBuilder
    var updateCmd = new CommandBuilder();
    updateCmd.Add($"UPDATE [{tableName}] SET ");
    // ... build SET clauses with {N} placeholders ...
    updateCmd.Add($" WHERE [{nameColumn}] = {{0}}", nameValue);
    Database.ExecuteNonQuery(updateCmd);
}
else
{
    // INSERT with parameterized CommandBuilder
    var insertCmd = new CommandBuilder();
    insertCmd.Add($"INSERT INTO [{tableName}] (...)  VALUES (...)");
    Database.ExecuteNonQuery(insertCmd);
}
```

**Key facts (HIGH confidence, from DW10 training docs):**
- `CommandBuilder` handles parameterization -- `{0}`, `{1}` placeholders become `@p0`, `@p1` SQL parameters
- `Database.CreateDataReader()` returns `IDataReader` -- standard ADO.NET pattern
- `Database.ExecuteNonQuery()` returns affected row count
- `Database.ExecuteScalar()` returns single value
- No connection management needed -- `Database` class handles connection pool internally

### SettingsProvider -- `Dynamicweb.Configuration` Namespace

For ~20 settings-based data groups. Each has `KeyPatterns` like `/Globalsettings/Modules/Users`.

**Serialize (read settings):**
```csharp
using Dynamicweb.Configuration;

// KeyPatterns from config, comma-separated
var patterns = "/Globalsettings/Modules/Users,/Globalsettings/Modules/UserManagement";
foreach (var pattern in patterns.Split(','))
{
    var value = SystemConfiguration.Instance.GetValue(pattern.Trim());
    // Serialize key-value pairs to YAML
}
```

**Deserialize (write settings):**
```csharp
SystemConfiguration.Instance.SetValue("/Globalsettings/Modules/Users/Setting1", "value");
```

**Key facts (MEDIUM confidence -- API exists but bulk enumeration needs investigation):**
- `SystemConfiguration.Instance.GetValue(path)` returns string value for exact path
- `SystemConfiguration.Instance.SetValue(path, value)` persists to GlobalSettings.config
- Need to investigate at implementation time: How to enumerate all child keys under a pattern prefix. May need direct XML parsing of `GlobalSettings.config` as fallback.

### SchemaProvider -- `Dynamicweb.Data` Namespace

For ~5 schema data groups. Exports SQL table column definitions.

**Serialize (read schema):**
```csharp
// Use SQL Server INFORMATION_SCHEMA to read table structure
var cmd = CommandBuilder.Create(
    "SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE " +
    "FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = {0}", tableName);
using var reader = Database.CreateDataReader(cmd);
```

**Deserialize (apply schema -- additive only):**
```csharp
// ALTER TABLE to add missing columns -- never drop columns (safety)
var alterCmd = CommandBuilder.Create(
    $"ALTER TABLE [{tableName}] ADD [{columnName}] [{dataType}]");
Database.ExecuteNonQuery(alterCmd);
```

**Key facts (HIGH confidence -- standard SQL Server metadata):**
- `INFORMATION_SCHEMA.COLUMNS` is reliable for column metadata
- Schema deserialization should be additive-only (add columns, never drop)
- This matches DW10's own `UpdateProvider` pattern for safe schema evolution

### ContentProvider -- Already Built

The existing `ContentSerializer`/`ContentDeserializer` use `Services.Pages`, `Services.Grids`, `Services.Paragraphs`, `Services.Items`. These wrap into the provider interface with no new APIs.

## Admin UI Patterns for Log Viewer

The log viewer uses the existing DW10 CoreUI list screen pattern.

**Screen type:** `ListScreenBase<LogEntryModel>` -- table with timestamp, level, message columns.

**Data source:** Parse `Files/System/ContentSync/Log/ContentSync.log`. The existing log format is `[timestamp] message`. For guided advice, the provider pipeline writes structured markers like `[ADVICE:MISSING_GROUP:Account Admin]` that the log viewer parses and renders as actionable guidance.

**No new infrastructure needed.** The existing `ListScreenBase` pattern (proven in `PredicateListScreen`) provides filtering, pagination, and context actions. The log viewer is a read-only list screen.

**Navigation:** Register under `Settings > Database > Serialize > Log` using `NavigationNodeProvider` (same pattern as existing `SyncSettingsNodeProvider`).

## Configuration Extension for v2.0

The existing `ContentSync.config.json` (renamed to `Serializer.config.json`) extends with provider definitions:

```json
{
  "providers": [
    {
      "type": "SqlTable",
      "name": "EcomOrderFlow",
      "table": "EcomOrderFlow",
      "nameColumn": "OrderFlowName",
      "enabled": true
    },
    {
      "type": "Settings",
      "name": "LanguageManagement",
      "keyPatterns": ["/Globalsettings/Modules/LanguageManagement"],
      "enabled": true
    },
    {
      "type": "Schema",
      "name": "EcomProducts_Schema",
      "table": "EcomProducts",
      "enabled": true
    },
    {
      "type": "Content",
      "name": "ContentSync",
      "predicates": ["...existing predicate format..."],
      "enabled": true
    }
  ]
}
```

Uses the existing `Microsoft.Extensions.Configuration.Json` package -- no new dependency.

## Version Compatibility

| Package | Current | Latest Stable | Action |
|---------|---------|---------------|--------|
| Dynamicweb | 10.23.9 | 10.23.9+ | Keep. Only upgrade if specific API is missing. |
| Dynamicweb.Content.UI | 10.23.9 | 10.23.9+ | Keep. |
| Dynamicweb.CoreUI.Rendering | 10.23.9 | 10.23.9+ | Keep. |
| YamlDotNet | 13.7.1 | 16.x | **Keep 13.7.1.** v14+ has breaking API changes. Upgrade is unnecessary risk. |
| Microsoft.Extensions.Configuration.Json | 8.0.1 | 8.0.1 | Keep. Matches .NET 8.0 SDK. |

**YamlDotNet version note (HIGH confidence):** YamlDotNet 14.0.0 introduced breaking changes to the serializer API. The project uses `ISerializer`, `IDeserializer`, `SerializerBuilder`, and `DeserializerBuilder` which work on 13.x but changed in 14.x. Staying on 13.7.1 avoids unnecessary rework with no benefit.

## Installation

No new packages to install. The existing `.csproj` already has everything needed.

If renaming from `Dynamicweb.ContentSync` to `DynamicWeb.Serializer`:

```xml
<!-- Only changes to .csproj -->
<RootNamespace>DynamicWeb.Serializer</RootNamespace>
<AssemblyName>DynamicWeb.Serializer</AssemblyName>
<PackageId>DynamicWeb.Serializer</PackageId>
```

## Sources

- [DW10 Core Concepts Training -- CommandBuilder, Database class](https://doc.dynamicweb.com/training/training/certifications/t3-platform-developer/t3-platform-developer/3-1-core-concepts) -- HIGH confidence
- [Database.CreateDataReader API docs](https://doc.dynamicweb.com/api/html/3f81ddaa-7137-def0-bb93-83f274fe73f9.htm) -- HIGH confidence
- [SystemConfiguration API docs](http://doc.dynamicweb.com/api/html/a150d119-f56f-9f3a-1995-572597c924fc.htm) -- HIGH confidence
- [DW10 AppStore App Guide -- full CRUD pattern with CommandBuilder](https://doc.dynamicweb.dev/documentation/extending/guides/newappstoreapp.html) -- HIGH confidence
- [DW10 Screen Types documentation](https://doc.dynamicweb.dev/documentation/extending/administration-ui/screentypes.html) -- HIGH confidence
- [Dynamicweb.Core NuGet -- confirms Database/CommandBuilder APIs](https://www.nuget.org/packages/Dynamicweb.Core/10.19.14) -- HIGH confidence
- [Dynamicweb.Configuration Namespace docs](https://doc.dynamicweb.com/api/html/1ec943e0-397f-74c3-550c-b9195922a2db.htm) -- MEDIUM confidence
- DataGroup XML files at `C:\temp\DataGroups\` -- direct inspection, HIGH confidence

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| SQL access via CommandBuilder/Database | HIGH | Official training docs + AppStore guide + API reference all confirm |
| Settings access via SystemConfiguration | MEDIUM | API exists and documented; bulk key enumeration under prefix needs runtime validation |
| Admin UI via CoreUI screens | HIGH | Already proven in v1.x with ListScreenBase and EditScreenBase |
| YamlDotNet compatibility | HIGH | Already working in v1.x; no upgrade needed |
| No new NuGet packages | HIGH | All required APIs ship with existing Dynamicweb 10.23.9 |
| Schema via INFORMATION_SCHEMA | HIGH | Standard SQL Server metadata pattern |

## Open Questions for Phase-Specific Research

1. **Settings key enumeration:** Can `SystemConfiguration.Instance` enumerate all child keys under a path prefix (e.g., list all keys under `/Globalsettings/Modules/Users/`)? Or do we need to parse the raw `GlobalSettings.config` XML file? Affects SettingsProvider implementation.

2. **ServiceCaches from DataGroup XMLs:** Some DataGroups declare `<ServiceCache>` entries (e.g., `Dynamicweb.Ecommerce.Orders.Discounts.DiscountService`). After deserialization, should we clear these caches? If so, how? Post-deserialize concern.

3. **Transaction scope for SQL deserialization:** The `Database` class supports transactions via `Database.CreateConnection()` + `connection.BeginTransaction()`. Should SqlTableProvider wrap each table's deserialization in a transaction for atomicity? Needs testing with large tables.
