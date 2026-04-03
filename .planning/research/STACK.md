# Technology Stack: Internal Link Resolution

**Project:** DynamicWeb.Serializer v0.3.1 -- Internal Link Resolution
**Researched:** 2026-04-02

## Recommended Stack

### No New Dependencies Required

The internal link resolution feature requires ZERO new NuGet packages or libraries. Everything needed is already available in the existing stack.

### DynamicWeb APIs to Use

| API | Namespace | Purpose | Why |
|-----|-----------|---------|-----|
| `LinkHelper.GetInternalPageIdsFromText(string)` | `Dynamicweb.Environment.Helpers` | Extract ALL page IDs from a text/HTML string | Returns `List<int>` -- handles all DW link formats automatically |
| `LinkHelper.IsLinkInternal(string)` | `Dynamicweb.Environment.Helpers` | Check if a single URL is an internal page link | Guards against rewriting external URLs |
| `LinkHelper.GetInternalPageId(string)` | `Dynamicweb.Environment.Helpers` | Extract page ID from a single internal URL | For targeted single-link fields (LinkEditor values) |
| `LinkHelper.IsLinkInternalParagraph(string?)` | `Dynamicweb.Environment.Helpers` | Check if URL contains paragraph anchor | Detects `Default.aspx?ID=NNN#PPP` format |
| `LinkHelper.GetInternalParagraphId(string?)` | `Dynamicweb.Environment.Helpers` | Extract paragraph ID from URL fragment | For paragraph anchor resolution |
| `LinkHelper.IsLinkInternalProductOrGroup(string)` | `Dynamicweb.Environment.Helpers` | Check if URL is a product/group link | Guard: do NOT rewrite these |
| `Services.Pages.GetPage(int)` | `Dynamicweb.Content` | Resolve page ID to Page object (for GUID lookup) | Already used throughout codebase |

### Existing Codebase Components to Extend

| Component | File | Extension Needed |
|-----------|------|-----------------|
| `ContentDeserializer` | `Serialization/ContentDeserializer.cs` | Add link resolution step in `SaveItemFields` |
| `WriteContext` | `Serialization/ContentDeserializer.cs` | Already has `PageGuidCache` (GUID->targetID) -- add source ID map |
| `ContentMapper` | `Serialization/ContentMapper.cs` | Serialize source page numeric IDs into YAML |
| `ReferenceResolver` | `Serialization/ReferenceResolver.cs` | Already resolves numeric IDs to GUIDs -- reuse pattern |
| `SerializedPage` | `Models/SerializedPage.cs` | Add `SourcePageId` field for the ID map |

### String Replacement Approach

Use `string.Replace` (not regex) for the actual rewriting. The pattern is always:

```
Default.aspx?ID={sourceId}  -->  Default.aspx?ID={targetId}
```

`LinkHelper.GetInternalPageIdsFromText` finds the IDs. Then simple string replacement rewrites them. No regex needed because:
1. The ID is always a decimal integer
2. The prefix `Default.aspx?ID=` is fixed
3. DW guarantees this format for all internal page links

### For Rich Text Fields Containing HTML

The same approach works. `GetInternalPageIdsFromText` parses through HTML and finds all `Default.aspx?ID=NNN` occurrences regardless of surrounding markup (`<a href="...">`, inline text, etc.).

## Alternatives Considered

| Category | Recommended | Alternative | Why Not |
|----------|-------------|-------------|---------|
| Link detection | `LinkHelper.GetInternalPageIdsFromText` | Custom regex `Default\.aspx\?ID=(\d+)` | DW API handles edge cases (query params, anchors, encoding) we'd miss |
| String replacement | `string.Replace` per ID | Regex `Regex.Replace` with callback | Simpler, faster, no regex compilation; IDs are always exact strings |
| ID mapping | Serialize source IDs into YAML | Query source DB at deserialization time | Source DB not available during deserialization (different environment) |
| Resolution timing | Post-deserialization pass | Inline during `SaveItemFields` | Need complete page GUID cache first; inline would miss pages not yet deserialized |

## Existing Stack (No Changes)

```
.NET 8.0
DynamicWeb 10.23.9 (includes Dynamicweb.Environment.Helpers.LinkHelper)
YamlDotNet (for YAML serialization)
System.IO.Compression (for zip handling)
```

## Sources

- [LinkHelper API reference](https://doc.dynamicweb.dev/api/Dynamicweb.Environment.Helpers.LinkHelper.html) -- HIGH confidence, official DW10 API docs
- [DynamicWeb Customized URLs](https://doc.dynamicweb.com/documentation-9/platform/platform-tools/customized-urls) -- HIGH confidence, official docs confirming `Default.aspx?ID=` as canonical internal link format
- [DynamicWeb Field Types](https://doc.dynamicweb.dev/manual/dynamicweb10/settings/areas/content/field-types.html) -- HIGH confidence, official docs on LinkEditor field type
- Codebase analysis of `ContentDeserializer.cs`, `ContentMapper.cs`, `ReferenceResolver.cs` -- HIGH confidence, direct source
