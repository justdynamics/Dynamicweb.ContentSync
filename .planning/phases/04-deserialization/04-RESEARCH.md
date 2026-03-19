# Phase 4: Deserialization - Research

**Researched:** 2026-03-19
**Domain:** DynamicWeb content write pipeline (YAML → DW database)
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Update Behavior**
- All fields overwritten on GUID match — full replica, consistent with source-wins philosophy
- System fields (CreatedDate, CreatedBy, UpdatedDate, UpdatedBy) included in updates — target mirrors source exactly
- ItemType custom fields (Dictionary<string, object>) use full replace — clear target's fields, write all from YAML. If a field exists in target but not in YAML, it gets removed
- New items (no GUID match): attempt to set system fields from YAML, not just content fields. May require post-save update if DW auto-assigns on insert

**Failure Handling**
- Continue-and-report on individual item failure — log error with item GUID and context, skip failed item, continue with remaining items
- Structured error summary at end: X succeeded, Y failed, Z skipped
- Cascade skip: if a parent page fails, skip all its children (they'd be orphaned). Log as "skipped due to parent failure"
- Error summary satisfies ROADMAP success criterion "rolled back or clearly reported" — no rollback mechanism needed
- DW transaction support is unverified (open blocker) — research must determine if PageService/GridService support transactions

**Dry-Run Mode (DES-04)**
- Full field-level diffs: for UPDATE items, show exactly which fields differ between YAML and current DB state
- Only show changed fields — omit unchanged fields for compact, focused output
- For CREATE items: list all fields being set (no diff, since item doesn't exist)
- For SKIP items: note "unchanged" with GUID
- Output through structured logging (ILogger) — same infrastructure as real runs, works with DW log viewer
- Dry-run requires reading current DB state for each matching GUID to compute field-level diffs

**Orphan Items**
- Ignore completely in v1 — don't touch items in DB that aren't in YAML files
- No orphan detection, no warnings, no tracking infrastructure
- OPS-04 (orphan handling) is explicitly v2 scope
- Minimal design — no pre-built orphan tracking, YAGNI

### Claude's Discretion
- GUID→numeric ID reference resolution approach (two-pass vs single-pass with deferred refs)
- Exact DW API method signatures for creating/updating pages, grid rows, paragraphs
- How to resolve DW services for writing (Services.Pages, etc.)
- Whether DW supports setting PageUniqueId on insert or if it's auto-generated
- Integration test structure and assertions for verifying write results

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| DES-01 | Deserialize YAML files back into DynamicWeb database | ContentDeserializer class using FileSystemStore.ReadTree() as input, then walking the DTO tree to write to DW via Services.Pages/Grids/Paragraphs |
| DES-02 | GUID-based identity — match on PageUniqueId, insert with new numeric ID if no match | Identity resolution via Services.Pages.GetPages() LINQ filter by UniqueId; no native GUID lookup API exists |
| DES-03 | Dependency-ordered writes — parent pages exist before children are inserted | Natural from tree traversal: area→pages (top-down recursion)→grid rows→paragraphs; parent numeric ID tracked in a write-context dictionary |
| DES-04 | Dry-run mode — report what would change without applying | isDryRun flag threaded through deserializer; reads DB state for comparison but calls no Save methods |
</phase_requirements>

---

## Summary

Phase 4 builds the inverse of Phase 3: instead of DW→YAML, it writes YAML→DW. The pipeline reads YAML files from disk via `FileSystemStore.ReadTree()` (already implemented and tested), walks the resulting DTO tree in dependency order, resolves each item's identity against the target DW database, and calls the appropriate Save service method.

The central technical finding is that **DynamicWeb's PageService has no GUID-based lookup**. `GetPage(int)` is the only overload; there is no `GetPage(Guid)` or `GetPageByUniqueId()`. Identity resolution therefore requires loading all pages for an area and filtering by `UniqueId` in LINQ. For grid rows and paragraphs the same situation applies — no GUID lookup API exists. This is the most architecturally significant constraint and directly shapes how the `ReferenceResolver` must be extended for the reverse direction.

The write API is straightforward: `SavePage(Page)` returns the saved `Page` (including its DW-assigned numeric ID on insert), `SaveGridRow(GridRow)` returns `bool`, and `SaveParagraph(Paragraph)` via the Paragraphs service returns `bool`. All three APIs set `UniqueId` on the object before saving — this is the mechanism for preserving GUID identity. No transaction API exists across these three services; atomicity is handled through the continue-and-report error strategy agreed in CONTEXT.md.

**Primary recommendation:** Implement `ContentDeserializer` as a direct peer of `ContentSerializer`, using the same constructor injection pattern (config, store, log). Walk the DTO tree returned by `ReadTree()` recursively in Area→Page→GridRow→Paragraph order, pre-build a GUID→numericId cache for the target area at the start of each predicate run, and thread a `writeContext` dictionary to track newly inserted numeric IDs for use as parent references.

---

## Standard Stack

### Core (all already in project — no new dependencies)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Dynamicweb.dll (lib/) | DW 10 | PageService, GridService, Paragraphs service | Same DLL used by serializer; copied from Swift2.1 bin |
| Dynamicweb.Core.dll (lib/) | DW 10 | Services static accessor, Item API | Same pattern as serializer |
| YamlDotNet | 13.7.1 | YAML deserialization (ReadTree already uses it) | Must stay at 13.7.1 to match DW bundled version — do NOT upgrade |
| xunit 2.9.3 | 2.9.3 | Integration tests | Same test project (`Dynamicweb.ContentSync.IntegrationTests`) |

**No new NuGet dependencies required for Phase 4.** All infrastructure is in place from Phase 3.

### Write API Summary (verified against official docs)

| Service | Write Method | Returns | Notes |
|---------|-------------|---------|-------|
| `Services.Pages` | `SavePage(Page page)` | `Page` | Returns saved page with DW-assigned numeric ID; also overload `SavePage(Page, bool skipLanguages)` |
| `Services.Grids` | `SaveGridRow(GridRow gridRow)` | `bool` | No numeric ID return — must re-query by PageId after insert if new ID needed |
| `Services.Paragraphs` | `SaveParagraph(Paragraph para)` | `bool` | Same — no ID return; ID must be set before save or re-queried |

### Identity Lookup Strategy (no native GUID API)

There is **no** `GetPage(Guid)`, `GetGridRowByUniqueId(Guid)`, or `GetParagraphByUniqueId(Guid)` method in DW10. The resolution approach is:

**Pages:** Load all pages for the target area once (via `Services.Pages.GetPagesByAreaID(areaId)`), build a `Dictionary<Guid, int>` mapping `page.UniqueId → page.ID`. O(1) lookups thereafter.

**Grid rows:** Load all grid rows for a page (via `Services.Grids.GetGridRowsByPageId(pageId)`), build a `Dictionary<Guid, int>` per page. Called once per page being processed.

**Paragraphs:** Load all paragraphs for a page (via `Services.Paragraphs.GetParagraphsByPageId(pageId)`), build a `Dictionary<Guid, int>` per page. Called once per page being processed.

**Confidence:** HIGH — verified against official DW10 API docs (method list complete with no GUID overloads found).

---

## Architecture Patterns

### Recommended Project Structure (additions for Phase 4)

```
src/Dynamicweb.ContentSync/
├── Serialization/
│   ├── ContentSerializer.cs          (Phase 3 — existing)
│   ├── ContentDeserializer.cs        (Phase 4 — new, peer class)
│   ├── ContentMapper.cs              (Phase 3 — existing; inverse mapping added here)
│   └── ReferenceResolver.cs          (Phase 3 — existing; extend for reverse direction)
├── ScheduledTasks/
│   ├── SerializeScheduledTask.cs     (Phase 3 — existing)
│   └── DeserializeScheduledTask.cs   (Phase 4 — new, mirrors serialize task)
tests/Dynamicweb.ContentSync.IntegrationTests/
└── Deserialization/
    └── CustomerCenterDeserializationTests.cs  (Phase 4 — new)
```

### Pattern 1: ContentDeserializer — Mirror of ContentSerializer

**What:** `ContentDeserializer` reads the DTO tree via `FileSystemStore.ReadTree()` and walks it in dependency order, writing to DW. Constructor signature mirrors `ContentSerializer`.

**Constructor:**
```csharp
public class ContentDeserializer
{
    private readonly SyncConfiguration _configuration;
    private readonly IContentStore _store;
    private readonly Action<string>? _log;
    private readonly bool _isDryRun;

    public ContentDeserializer(
        SyncConfiguration configuration,
        IContentStore? store = null,
        Action<string>? log = null,
        bool isDryRun = false)
    {
        _configuration = configuration;
        _store = store ?? new FileSystemStore();
        _log = log;
        _isDryRun = isDryRun;
    }

    public DeserializeResult Deserialize() { ... }
}
```

**Return type:** A `DeserializeResult` value type (record) carrying `int Created, int Updated, int Skipped, int Failed, IReadOnlyList<string> Errors`.

### Pattern 2: GUID Identity Pre-Caching (Single-Pass)

**What:** Before processing any items for a predicate, load all existing pages for the target area into a lookup dictionary. This avoids per-item service calls during the write loop.

**When to use:** At the start of `DeserializePredicate()`, before walking the DTO tree.

```csharp
// Source: DW10 PageService verified API
private Dictionary<Guid, int> BuildPageGuidCache(int areaId)
{
    var allPages = Services.Pages.GetPagesByAreaID(areaId);
    return allPages
        .Where(p => p.UniqueId != Guid.Empty)
        .ToDictionary(p => p.UniqueId, p => p.ID);
}
```

**For grid rows and paragraphs:** Build per-page at write time (load once per page, not once per item):

```csharp
private Dictionary<Guid, int> BuildGridRowGuidCache(int pageId) =>
    Services.Grids.GetGridRowsByPageId(pageId)
        .Where(gr => gr.UniqueId != Guid.Empty)
        .ToDictionary(gr => gr.UniqueId, gr => gr.ID);

private Dictionary<Guid, int> BuildParagraphGuidCache(int pageId) =>
    Services.Paragraphs.GetParagraphsByPageId(pageId)
        .Where(p => p.UniqueId != Guid.Empty)
        .ToDictionary(p => p.UniqueId, p => p.ID);
```

### Pattern 3: Write Context — Tracking Parent IDs Across Tree

**What:** When a new page is inserted, DW assigns a numeric ID. That numeric ID must be available when writing the page's grid rows (which need PageId) and child pages (which need ParentPageId). A `WriteContext` carries this mapping through the recursive walk.

**Structure:**

```csharp
// Passed through recursive calls; not shared across predicate runs
private class WriteContext
{
    public int TargetAreaId { get; set; }
    public int ParentPageId { get; set; }   // 0 for root pages
    public Dictionary<Guid, int> PageGuidCache { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
}
```

**Dependency order enforcement:**

```
DeserializePredicate(predicate)
  -> BuildPageGuidCache(areaId)          // Pre-load existing page GUIDs
  -> foreach rootPage in area.Pages
       -> DeserializePage(page, ctx)     // Returns resolved numeric pageId
            -> if failed: add to ctx.Errors, return -1 (signals cascade skip)
            -> SavePage / no-op in dry-run
            -> record new numericId in ctx.PageGuidCache[page.Guid] = newId
            -> BuildGridRowGuidCache(resolvedPageId)
            -> foreach gridRow in page.GridRows
                 -> DeserializeGridRow(row, pageId, ctx)
                      -> BuildParagraphGuidCache(resolvedPageId)
                      -> foreach para in row.Columns[*].Paragraphs
                           -> DeserializeParagraph(para, gridRowId, columnId, pageId, ctx)
            -> foreach child in page.Children
                 -> ctx.ParentPageId = resolvedPageId
                 -> DeserializePage(child, ctx)   // Recurse
```

### Pattern 4: DTO→DW Mapping (Reverse ContentMapper)

**What:** Map each DTO record to its DW object for Save calls. This is the inverse of `ContentMapper`'s DW→DTO mapping.

**Page mapping:**
```csharp
// Source: DW10 Page property API (verified: UniqueId, AreaId, ParentPageId, MenuText,
//         UrlName, Active, Sort are all settable)
private Page MapToPage(SerializedPage dto, int areaId, int parentPageId, int? existingNumericId)
{
    var page = new Page();
    page.UniqueId = dto.PageUniqueId;         // Preserved on both insert and update
    page.AreaId = areaId;
    page.ParentPageId = parentPageId;
    page.MenuText = dto.MenuText;
    page.UrlName = dto.UrlName;
    page.Active = dto.IsActive;
    page.Sort = dto.SortOrder;

    if (existingNumericId.HasValue)
        page.ID = existingNumericId.Value;    // Set for UPDATE path

    // Apply ItemType fields
    if (page.Item != null)
    {
        foreach (var kvp in dto.Fields)
            page.Item[kvp.Key] = kvp.Value;
    }

    return page;
}
```

**Grid row mapping:**
```csharp
// Source: DW10 GridRow property API (verified: UniqueId, PageId, Sort settable)
private GridRow MapToGridRow(SerializedGridRow dto, int pageId, int? existingNumericId)
{
    var row = new GridRow(pageId);
    row.UniqueId = dto.Id;
    row.Sort = dto.SortOrder;

    if (existingNumericId.HasValue)
        row.ID = existingNumericId.Value;

    return row;
}
```

**Paragraph mapping:**
```csharp
// Source: DW10 Paragraph property API (verified: UniqueId, ID, GridRowId, GridRowColumn,
//         PageID, Sort, Header, ItemType, Text all settable)
private Paragraph MapToParagraph(SerializedParagraph dto, int pageId, int gridRowId,
    int columnId, int? existingNumericId)
{
    var para = new Paragraph();
    para.UniqueId = dto.ParagraphUniqueId;
    para.PageID = pageId;
    para.GridRowId = gridRowId;
    para.GridRowColumn = columnId;
    para.Sort = dto.SortOrder;
    para.Header = dto.Header;
    para.ItemType = dto.ItemType;

    if (existingNumericId.HasValue)
        para.ID = existingNumericId.Value;

    // Apply fields
    if (para.Item != null)
    {
        // Full replace: clear existing fields not in YAML
        foreach (var existingField in para.Item.Names.ToList())
            para.Item[existingField] = null;
        foreach (var kvp in dto.Fields)
        {
            if (kvp.Key == "Text")
                para.Text = kvp.Value?.ToString();
            else
                para.Item[kvp.Key] = kvp.Value;
        }
    }

    return para;
}
```

### Pattern 5: Dry-Run Field Diff

**What:** For UPDATE items in dry-run mode, compare YAML field values against current DB state and log only changed fields.

**When to use:** Whenever `_isDryRun = true` AND the item has an existing numeric ID (is an UPDATE, not a CREATE).

```csharp
private void LogDryRunUpdate(SerializedPage yamlDto, Page existingPage)
{
    var diffs = new List<string>();

    if (yamlDto.MenuText != existingPage.MenuText)
        diffs.Add($"MenuText: '{existingPage.MenuText}' -> '{yamlDto.MenuText}'");
    if (yamlDto.IsActive != existingPage.Active)
        diffs.Add($"Active: {existingPage.Active} -> {yamlDto.IsActive}");

    // Field-level diffs for ItemType fields
    foreach (var kvp in yamlDto.Fields)
    {
        var currentVal = existingPage.Item?[kvp.Key]?.ToString();
        var newVal = kvp.Value?.ToString();
        if (currentVal != newVal)
            diffs.Add($"Fields[{kvp.Key}]: '{currentVal}' -> '{newVal}'");
    }

    if (diffs.Count == 0)
        Log($"[DRY-RUN] SKIP {yamlDto.PageUniqueId} (unchanged)");
    else
        Log($"[DRY-RUN] UPDATE {yamlDto.PageUniqueId}:\n  " + string.Join("\n  ", diffs));
}
```

### Pattern 6: DeserializeScheduledTask

**What:** Mirrors `SerializeScheduledTask` exactly — same config loading pattern, same log file approach.

```csharp
[AddInName("ContentSync.Deserialize")]
[AddInLabel("ContentSync - Deserialize")]
[AddInDescription("Deserializes YAML content files to DynamicWeb database based on ContentSync.config.json predicates.")]
public class DeserializeScheduledTask : BaseScheduledTaskAddIn
{
    public override bool Run()
    {
        // Same FindConfigFile() pattern as SerializeScheduledTask
        // Pass isDryRun: false for normal runs
        // Log result summary: X created, Y updated, Z skipped, W failed
    }
}
```

### Anti-Patterns to Avoid

- **Per-item GUID lookup via full table scan:** Don't call `Services.Pages.GetPages()` (loads ALL pages in the system) for each item. Pre-build area-scoped caches once per predicate run instead.
- **Writing children before parents:** Never call `SaveGridRow` before the parent `SavePage` has returned its numeric ID. Always track the returned/resolved `pageId` before processing that page's grid rows.
- **Using numeric IDs from YAML for DW writes:** The YAML files contain `SortOrder` and optional informational numeric IDs but these must NOT be used as `page.ID` for insert paths. Only use DW-returned IDs.
- **Catching all exceptions silently:** The continue-and-report strategy logs and counts failures. Do not swallow exceptions without recording them in `WriteContext.Errors`.
- **Clearing ItemType fields via direct assignment to null on Item:** DW's Item API behavior on null assignment is unverified. Use an explicit field-clearing approach by overwriting with empty string or the YAML-provided value, and verify against Swift2.1 integration test.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| YAML file reading | Custom YAML parser | `FileSystemStore.ReadTree()` | Already implemented, tested, handles all edge cases (CRLF, tilde, HTML) |
| Config loading | Custom JSON reader | `ConfigLoader.Load(path)` | Phase 2 implementation handles validation and error messages |
| Predicate filtering | Custom inclusion logic | `ContentPredicateSet.ShouldInclude()` | Same predicate logic applies to deserialization scope |
| Item field access | Direct property reflection | `page.Item[fieldName]` DW API | DW's Item API is the correct abstraction for ItemType fields |
| YAML deserialization of DTOs | Custom deserializer | `YamlConfiguration.BuildDeserializer()` | Already configured in Phase 3 for all DTO types |
| Log file writing | Custom file logger | Existing `Action<string>? _log` pattern | Same as SerializeScheduledTask; stays consistent |

**Key insight:** Phase 4's infrastructure cost is low — the heavy lifting (YAML I/O, config, predicates, DW service access patterns) is already in place. The new work is exclusively the write side: mapping DTOs back to DW objects and calling Save methods.

---

## Common Pitfalls

### Pitfall 1: SaveGridRow Does Not Return the New Numeric ID

**What goes wrong:** `Services.Grids.SaveGridRow(gridRow)` returns `bool`, not a `GridRow` with an assigned ID. After inserting a new grid row, you don't have its numeric ID to use as the `GridRowId` on paragraph saves.

**Why it happens:** Unlike `SavePage(Page)` which returns the full `Page` object with DW-assigned ID, `SaveGridRow` returns only success/failure.

**How to avoid:** After a successful new grid row insert, immediately re-query:
```csharp
Services.Grids.SaveGridRow(newRow);
// Re-query to get the DW-assigned numeric ID
var saved = Services.Grids.GetGridRowsByPageId(pageId)
    .FirstOrDefault(gr => gr.UniqueId == dto.Id);
var newGridRowId = saved?.ID ?? throw new InvalidOperationException(
    $"Could not find inserted grid row with GUID {dto.Id}");
```

**Warning signs:** `NullReferenceException` or "GridRowId = 0" when saving paragraphs.

### Pitfall 2: GetParagraphsByPageId Returns Only Active Paragraphs

**What goes wrong:** `Services.Paragraphs.GetParagraphsByPageId(pageId)` may return only active/published paragraphs, missing inactive ones. This means the GUID cache built for identity resolution is incomplete.

**Why it happens:** Forum-documented behavior (MEDIUM confidence — not officially verified). `bool` overload on `GetGridRowsByPageId(int, bool onlyActive)` suggests a similar pattern for paragraphs but it is unverified for `GetParagraphsByPageId`.

**How to avoid:** Verify empirically in the first integration test run. If inactive paragraphs are missing from the identity cache, they will be inserted as duplicates rather than updated in place. If this occurs, the workaround is to accept this limitation (Phase 4 works on active content) and document it, or research the inactive paragraph lookup before finalizing.

**Warning signs:** Deserialization creates duplicate paragraphs with the same GUID instead of updating existing ones.

### Pitfall 3: Page.Item May Be Null Before First Save

**What goes wrong:** For a new `Page()` object, `page.Item` may be null before it has been saved to DW (DW may not create the Item until after `SavePage()` is called).

**Why it happens:** The Item API is backed by DW's item system, which is initialized during page persist. The Item object is populated from DB state on page load.

**How to avoid:** After `SavePage()` for new pages, re-fetch the page with `Services.Pages.GetPage(newId)` before attempting to write ItemType fields, or apply fields as a second update pass. For update paths, the page was loaded from DB and already has Item initialized.

**Warning signs:** `NullReferenceException` on `page.Item[fieldName] = value`.

### Pitfall 4: Setting page.ID on Insert Causes Conflict

**What goes wrong:** Setting `page.ID` before `SavePage()` on an insert path (where no match was found) may cause DW to attempt an UPDATE of a non-existent record, or cause an ID collision.

**Why it happens:** DW's Save methods use `ID == 0` (or `ID <= 0`) to distinguish insert from update internally.

**How to avoid:** On the INSERT path, leave `page.ID = 0` (the default). Only set `page.ID = existingNumericId` on the UPDATE path.

**Warning signs:** `INSERT failed` or `duplicate key` exceptions from DW on pages that should be new.

### Pitfall 5: YamlDotNet Version Must Stay at 13.7.1

**What goes wrong:** Upgrading YamlDotNet to a newer version (e.g., 16.x) causes runtime conflicts with DW's bundled YamlDotNet (assembly version 13.0.0.0).

**Why it happens:** DW bundles YamlDotNet internally. Assembly binding conflicts at runtime in the DW host process.

**How to avoid:** Do not touch YamlDotNet version. Keep at 13.7.1 (assembly version 13.0.0.0). This constraint is already documented in memory/reference_test_instances.md.

### Pitfall 6: Dynamicweb.Environment Shadows System.Environment

**What goes wrong:** `System.Environment.NewLine` returns `Dynamicweb.Environment.NewLine` at runtime due to DW namespace shadowing.

**How to avoid:** Always use `"\n"` literal for newlines in log messages and string comparisons — never `Environment.NewLine`. Already documented in memory/reference_test_instances.md.

---

## Code Examples

### Complete Insert vs Update Decision

```csharp
// Source: DW10 PageService API (verified: SavePage returns Page with DW-assigned ID)
private int DeserializePage(SerializedPage dto, WriteContext ctx)
{
    if (!ctx.PageGuidCache.TryGetValue(dto.PageUniqueId, out var existingId))
    {
        // INSERT path
        if (_isDryRun)
        {
            Log($"[DRY-RUN] CREATE page {dto.PageUniqueId} ('{dto.MenuText}')");
            foreach (var f in dto.Fields)
                Log($"  set Fields[{f.Key}] = '{f.Value}'");
            return -1; // Synthetic ID for dry-run; children not processed
        }

        var page = MapToPage(dto, ctx.TargetAreaId, ctx.ParentPageId, existingNumericId: null);
        var saved = Services.Pages.SavePage(page);
        ctx.PageGuidCache[dto.PageUniqueId] = saved.ID; // Cache new ID
        ctx.Created++;
        Log($"CREATED page {dto.PageUniqueId} -> ID={saved.ID}");
        return saved.ID;
    }
    else
    {
        // UPDATE path
        if (_isDryRun)
        {
            var existing = Services.Pages.GetPage(existingId);
            LogDryRunUpdate(dto, existing);
            return existingId;
        }

        var page = MapToPage(dto, ctx.TargetAreaId, ctx.ParentPageId, existingNumericId: existingId);
        Services.Pages.SavePage(page);
        ctx.Updated++;
        Log($"UPDATED page {dto.PageUniqueId} (ID={existingId})");
        return existingId;
    }
}
```

### Error Handling with Cascade Skip

```csharp
// Continue-and-report: log error, skip children
private void DeserializePageSafe(SerializedPage dto, WriteContext ctx)
{
    try
    {
        int resolvedId = DeserializePage(dto, ctx);
        if (resolvedId < 0 && !_isDryRun) return; // Failed or dry-run

        // Process grid rows for this page
        var rowCache = BuildGridRowGuidCache(resolvedId);
        foreach (var row in dto.GridRows)
            DeserializeGridRowSafe(row, resolvedId, rowCache, ctx);

        // Recurse children with updated ParentPageId
        var childCtx = ctx with { ParentPageId = resolvedId };
        foreach (var child in dto.Children)
            DeserializePageSafe(child, childCtx);
    }
    catch (Exception ex)
    {
        ctx.Failed++;
        var msg = $"ERROR deserializing page {dto.PageUniqueId} ('{dto.MenuText}'): {ex.Message}";
        ctx.Errors.Add(msg);
        Log(msg);
        // Children skipped via implicit return — cascade skip behavior
        Log($"  SKIPPED children of {dto.PageUniqueId} due to parent failure");
    }
}
```

### DeserializeResult Return Type

```csharp
public record DeserializeResult
{
    public int Created { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public bool HasErrors => Failed > 0 || Errors.Count > 0;

    public string Summary =>
        $"Deserialization complete: {Created} created, {Updated} updated, " +
        $"{Skipped} skipped, {Failed} failed.";
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Returning numeric ID from SaveGridRow | SaveGridRow returns bool; re-query needed | DW10 API design | Must re-query after insert to get new grid row ID |
| GUID-based lookup API | No GUID lookup — must filter collection | DW10 API design | Pre-build area-level caches; do not do per-item full scans |
| Static `Paragraphs` service accessor | `Services.Paragraphs.GetParagraphsByPageId()` | Phase 3 pattern | Consistent with serializer — use same accessor pattern |

**Deprecated/outdated:**
- `new PageService()` constructor: Do NOT instantiate DW service classes directly. Use `Services.Pages` static accessor — this is the canonical DW10 pattern established in Phase 3.

---

## Open Questions

1. **Does setting `page.UniqueId` before `SavePage()` on an INSERT preserve the GUID in DW's database?**
   - What we know: `UniqueId` property is settable on `Page`, `GridRow`, and `Paragraph` (verified from official docs). The architecture docs assume this works (Pattern 3 in ARCHITECTURE.md shows this).
   - What's unclear: DW may overwrite `UniqueId` with a fresh GUID on insert, treating it as auto-generated.
   - Recommendation: Verify in the very first integration test: insert a page with a known GUID, re-fetch by numeric ID, assert `UniqueId == expectedGuid`. If DW overwrites the GUID, the entire identity strategy must be reconsidered.

2. **Does `GetParagraphsByPageId` return inactive paragraphs?**
   - What we know: Forum docs (MEDIUM confidence) suggest it returns only active paragraphs. The `bool` overload of `GetGridRowsByPageId(int, bool)` suggests inactive awareness exists in grid rows.
   - What's unclear: Whether there's an `includeInactive` overload for the Paragraphs service.
   - Recommendation: Verify empirically — serialize a page with an inactive paragraph, then check if it appears in the GUID cache during deserialization.

3. **Does DW support cross-service transactions (SavePage + SaveGridRow + SaveParagraph in one transaction)?**
   - What we know: No transaction API was found in official docs. No forum examples show cross-service transactions.
   - What's unclear: DW may use implicit database transactions internally per Save call.
   - Recommendation: Treat as unverified. The continue-and-report error strategy agreed in CONTEXT.md is the correct answer — no rollback mechanism needed. Log partial failures clearly.

4. **Does `page.Item` exist before first save for new Page objects?**
   - What we know: `Item` is populated from DB state when a page is loaded via `Services.Pages.GetPage()`. For a new `new Page()`, the backing Item may not exist.
   - What's unclear: Whether DW initializes the Item in memory at construction or only after DB persist.
   - Recommendation: Apply ItemType fields in a post-save update pass for INSERT paths: save the page first, re-fetch it, then update Item fields and save again. This adds one extra save call per new page but is safe.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 |
| Config file | No separate config — DLL deployment to Swift2.1 required before running |
| Quick run command | `dotnet test tests/Dynamicweb.ContentSync.IntegrationTests/ --filter "Category=Integration&Class=CustomerCenterDeserializationTests"` |
| Full suite command | `dotnet test tests/Dynamicweb.ContentSync.IntegrationTests/ --filter "Category=Integration"` |

**Critical constraint:** Integration tests require the DW runtime. Tests CANNOT be run standalone. Required sequence:
1. Build DLL: `dotnet build src/Dynamicweb.ContentSync/Dynamicweb.ContentSync.csproj -c Debug`
2. Stop Swift2.1 (DLL is locked while running)
3. Copy DLL to Swift2.1 `bin/Debug/net8.0/` (not net10.0 — Swift2.1 is .NET 8)
4. Start Swift2.1: `cd C:\Projects\Solutions\swift.test.forsync\Swift2.1\Dynamicweb.Host.Suite && dotnet run`
5. Run tests: `dotnet test tests/Dynamicweb.ContentSync.IntegrationTests/ --filter "Category=Integration"`

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| DES-01 | Deserializing Customer Center YAML into fresh instance B creates pages, grid rows, paragraphs | Integration | `dotnet test ... --filter "Category=Integration&FullyQualifiedName~Deserialize_CustomerCenter_CreatesExpectedItems"` | ❌ Wave 0 |
| DES-02 | Items matched by GUID are updated in place; items not present are inserted with new numeric IDs | Integration | `dotnet test ... --filter "Category=Integration&FullyQualifiedName~Deserialize_CustomerCenter_GuidIdentity"` | ❌ Wave 0 |
| DES-03 | Write order is Areas→Pages→GridRows→Paragraphs; no child inserted before parent | Integration (implicit in DES-01 — if order wrong, save calls fail) | Validated by test success itself | ❌ Wave 0 |
| DES-04 | Dry-run reports CREATE/UPDATE/SKIP without writing to DB | Integration | `dotnet test ... --filter "Category=Integration&FullyQualifiedName~Deserialize_DryRun_ReportsChanges"` | ❌ Wave 0 |

**Unit testable components (no DW runtime needed):**

| Behavior | Test Type | File Exists? |
|----------|-----------|-------------|
| `DeserializeResult.Summary` formatting | Unit | ❌ Wave 0 |
| `MapToPage` sets correct properties | Unit (stub Page) | ❌ Wave 0 |
| Dry-run diff logic on field values | Unit | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** No automated quick-run available (DW runtime required for all substantive tests)
- **Per wave merge:** Full integration suite against Swift2.1
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `tests/Dynamicweb.ContentSync.IntegrationTests/Deserialization/CustomerCenterDeserializationTests.cs` — covers DES-01, DES-02, DES-03, DES-04
- [ ] Unit test file for `ContentDeserializer` mapping logic (no DW runtime needed for pure mapping tests)

*(Test project and DLL references already exist — no new project scaffolding required. Wave 0 only needs test class files.)*

---

## Sources

### Primary (HIGH confidence)
- DW10 PageService official API docs (doc.dynamicweb.dev) — verified all method signatures; confirmed no GUID overload
- DW10 GridService official API docs (doc.dynamicweb.dev) — verified SaveGridRow returns bool, not GridRow
- DW10 Page property API docs (doc.dynamicweb.dev) — verified UniqueId, AreaId, ParentPageId, MenuText, UrlName, Active, Sort all settable
- DW10 GridRow property API docs (doc.dynamicweb.dev) — verified UniqueId, PageId, Sort settable; constructors `GridRow()` and `GridRow(int pageId)`
- DW10 Paragraph property API docs (doc.dynamicweb.dev) — verified UniqueId, GridRowId, GridRowColumn, PageID, Sort, Header, ItemType, Text all settable
- `src/Dynamicweb.ContentSync/Serialization/ContentSerializer.cs` — established constructor pattern, Services.Xxx accessor pattern
- `src/Dynamicweb.ContentSync/Serialization/ContentMapper.cs` — DW→DTO mapping to invert
- `src/Dynamicweb.ContentSync/Infrastructure/FileSystemStore.cs` — ReadTree() input to deserializer
- `src/Dynamicweb.ContentSync/ScheduledTasks/SerializeScheduledTask.cs` — exact pattern to mirror for DeserializeScheduledTask
- `memory/reference_test_instances.md` — Swift2.1 deployment path, YamlDotNet 13.7.1 constraint, Dynamicweb.Environment shadow warning

### Secondary (MEDIUM confidence)
- DW10 forum: Creating Pages and Paragraphs with the API (doc.dynamicweb.com/forum) — SaveParagraph usage pattern, ItemService.SaveItem for item fields
- `planning/research/ARCHITECTURE.md` — GUID identity pattern, deserialization data flow

### Tertiary (LOW confidence)
- Forum documentation: GetParagraphsByPageId returns only active paragraphs — single source, needs integration test verification

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new dependencies; all library versions verified from existing project
- Write API signatures: HIGH — verified against official DW10 API docs
- GUID identity approach: HIGH — verified no native GUID lookup exists; LINQ filter approach is the only option
- SaveGridRow ID return behavior: HIGH — returns bool, not GridRow (verified from official docs)
- ItemType field write behavior: MEDIUM — Item API pattern inferred from ContentMapper read pattern; null/clear behavior unverified
- Paragraph active/inactive behavior: LOW — single forum source, needs empirical verification

**Research date:** 2026-03-19
**Valid until:** 2026-06-19 (stable DW10 API; 90-day window)
