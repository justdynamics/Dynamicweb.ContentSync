# Architecture: Internal Link Resolution in ContentDeserializer

**Domain:** Internal page ID rewriting during content deserialization
**Researched:** 2026-04-02
**Confidence:** HIGH (based on direct codebase analysis of existing pipeline)

## Recommended Architecture

### Two-Phase Deserialization

The current `ContentDeserializer.Deserialize()` processes pages in tree order (parent before children), writing each page immediately. Link resolution requires knowing ALL target page IDs before rewriting any links. This means link resolution must happen as a **second pass** after all pages are deserialized.

```
Phase 1: Existing deserialization (unchanged)
  - Deserialize all pages, grid rows, paragraphs
  - Build PageGuidCache: { GUID -> targetPageId }
  - Save all Item fields WITHOUT link resolution

Phase 2: Link resolution pass (NEW)
  - Build sourcePageId -> targetPageId map using:
    - YAML serialized { sourcePageId, pageGUID } pairs
    - PageGuidCache { pageGUID -> targetPageId }
  - For each deserialized page/paragraph:
    - Scan all Item field values for Default.aspx?ID=NNN
    - Replace source IDs with target IDs
    - Re-save modified Item fields
```

### Why Two-Phase, Not Single-Pass

Single-pass link resolution would fail because:
1. A page's field may link to a page that hasn't been deserialized yet (forward reference)
2. The complete GUID-to-target-ID map isn't available until ALL pages are processed
3. Child pages may link to sibling branches in the tree

The two-phase approach guarantees the complete ID map exists before any resolution.

### Component Boundaries

| Component | Responsibility | Communicates With |
|-----------|---------------|-------------------|
| `InternalLinkResolver` (NEW) | Detects and rewrites `Default.aspx?ID=NNN` in string values | `LinkHelper` API, ID map dictionary |
| `ContentDeserializer` (MODIFIED) | Orchestrates two-phase deserialization | `InternalLinkResolver`, `WriteContext` |
| `ContentMapper` (MODIFIED) | Serializes source page numeric IDs alongside GUIDs | `SerializedPage` model |
| `SerializedPage` (MODIFIED) | Carries `SourcePageId` in YAML | YAML serialization |
| `WriteContext` (MODIFIED) | Tracks source-to-target ID map | `ContentDeserializer`, `InternalLinkResolver` |

### Data Flow

```
Serialization (source environment):
  Page.ID (numeric) + Page.UniqueId (GUID)
    --> SerializedPage { SourcePageId: 123, PageUniqueId: abc-def-... }
    --> YAML file on disk

Deserialization (target environment):
  1. Read YAML: { SourcePageId: 123, PageUniqueId: abc-def-... }
  2. Deserialize page: abc-def-... resolves to target ID 456
  3. Build map: { 123 -> 456 } (source page 123 is now page 456 on target)
  4. Scan all field values for "Default.aspx?ID=123"
  5. Replace with "Default.aspx?ID=456"
  6. Re-save item fields
```

## Patterns to Follow

### Pattern 1: InternalLinkResolver (Stateless Helper)

Follow the `PermissionMapper` pattern -- a focused helper class that the deserializer instantiates and calls.

```csharp
public class InternalLinkResolver
{
    private readonly Dictionary<int, int> _sourceToTargetPageIds;
    private readonly Action<string>? _log;
    private int _resolvedCount;
    private int _unresolvedCount;

    public InternalLinkResolver(
        Dictionary<int, int> sourceToTargetPageIds,
        Action<string>? log = null)
    {
        _sourceToTargetPageIds = sourceToTargetPageIds;
        _log = log;
    }

    /// <summary>
    /// Scans a field value string for Default.aspx?ID=NNN patterns and
    /// rewrites source page IDs to target page IDs.
    /// Returns the original string if no links found or no mapping exists.
    /// </summary>
    public string ResolveLinks(string fieldValue)
    {
        var pageIds = LinkHelper.GetInternalPageIdsFromText(fieldValue);
        if (pageIds == null || pageIds.Count == 0)
            return fieldValue;

        var result = fieldValue;
        foreach (var sourceId in pageIds)
        {
            if (_sourceToTargetPageIds.TryGetValue(sourceId, out var targetId))
            {
                result = result.Replace(
                    $"Default.aspx?ID={sourceId}",
                    $"Default.aspx?ID={targetId}");
                _resolvedCount++;
            }
            else
            {
                _log?.Invoke($"  WARNING: Unresolvable page ID {sourceId} in link — leaving unchanged");
                _unresolvedCount++;
            }
        }
        return result;
    }

    public (int resolved, int unresolved) GetStats() => (_resolvedCount, _unresolvedCount);
}
```

### Pattern 2: Source ID Map in WriteContext

Extend the existing `WriteContext` to accumulate the source-to-target mapping during Phase 1.

```csharp
private class WriteContext
{
    // ... existing fields ...
    public Dictionary<Guid, int> PageGuidCache { get; set; } = new();

    // NEW: source page IDs from YAML, keyed by GUID
    public Dictionary<Guid, int> SourcePageIds { get; set; } = new();
}
```

After Phase 1 completes, build the final map:
```csharp
var sourceToTarget = new Dictionary<int, int>();
foreach (var (guid, sourceId) in ctx.SourcePageIds)
{
    if (ctx.PageGuidCache.TryGetValue(guid, out var targetId))
        sourceToTarget[sourceId] = targetId;
}
```

### Pattern 3: Field Value Scanning

Apply link resolution to ALL string-typed Item field values. Don't try to detect which fields are "link fields" vs "text fields" -- just scan everything. Reasons:
1. `GetInternalPageIdsFromText` is cheap (returns empty list quickly for non-link text)
2. Editors can paste links into any text field
3. Avoids maintaining a fragile allowlist of field types

```csharp
private void SaveItemFieldsWithLinkResolution(
    string? itemType, string itemId,
    Dictionary<string, object> fields,
    InternalLinkResolver? linkResolver)
{
    // ... existing field loading logic ...

    foreach (var kvp in contentFields)
    {
        if (linkResolver != null && kvp.Value is string strValue && strValue.Length > 0)
        {
            var resolved = linkResolver.ResolveLinks(strValue);
            if (resolved != strValue)
            {
                contentFields[kvp.Key] = resolved;
                Log($"  Resolved links in {kvp.Key}");
            }
        }
    }

    // ... existing DeserializeFrom + Save ...
}
```

## Anti-Patterns to Avoid

### Anti-Pattern 1: Regex-Only Link Detection
**What:** Writing custom regex like `Default\.aspx\?ID=(\d+)` to find links.
**Why bad:** DW's `LinkHelper.GetInternalPageIdsFromText` handles URL encoding, case variations, and edge cases. Custom regex would miss `default.aspx?id=123` (case insensitive) or `Default.aspx?ID=123&GroupID=G1`.
**Instead:** Use `LinkHelper.GetInternalPageIdsFromText` for detection, `string.Replace` for rewriting.

### Anti-Pattern 2: Single-Pass Resolution
**What:** Trying to resolve links while deserializing each page.
**Why bad:** Forward references. Page A links to Page B, but B hasn't been deserialized yet.
**Instead:** Two-phase: deserialize all pages first, then resolve all links.

### Anti-Pattern 3: Field Type Allowlist
**What:** Only scanning fields marked as "Link" or "Button" type.
**Why bad:** Users paste links into text fields, HTML fields, and other arbitrary fields.
**Instead:** Scan ALL string field values. The detection API is fast and returns empty for non-links.

### Anti-Pattern 4: Modifying YAML During Resolution
**What:** Rewriting links in the YAML files on disk.
**Why bad:** YAML files represent the source environment. They should remain unchanged -- the source IDs are correct for the source environment.
**Instead:** Resolve during deserialization only. YAML stays pristine as source of truth.

## Integration Point

The link resolution phase hooks into `ContentDeserializer.DeserializePredicate()` after the main page tree walk:

```csharp
private DeserializeResult DeserializePredicate(ProviderPredicateDefinition predicate, SerializedArea area)
{
    // ... existing setup ...

    // Phase 1: Deserialize all pages (existing code, with SourcePageId tracking)
    foreach (var page in area.Pages)
    {
        DeserializePageSafe(page, ctx);
    }

    // Phase 2: Resolve internal links (NEW)
    var sourceToTarget = BuildSourceToTargetMap(ctx);
    if (sourceToTarget.Count > 0)
    {
        var resolver = new InternalLinkResolver(sourceToTarget, _log);
        ResolveLinksInArea(predicate.AreaId, resolver);
        var (resolved, unresolved) = resolver.GetStats();
        Log($"Link resolution: {resolved} links resolved, {unresolved} unresolvable");
    }

    // ... existing result building ...
}
```

## Sources

- Direct analysis of `ContentDeserializer.cs` (940 lines), `ContentMapper.cs`, `ReferenceResolver.cs`
- [LinkHelper API](https://doc.dynamicweb.dev/api/Dynamicweb.Environment.Helpers.LinkHelper.html) -- method signatures confirmed
- PermissionMapper pattern -- established project convention for cross-cutting deserialization concerns
