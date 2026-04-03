# Feature Landscape: Internal Link Resolution

**Domain:** Internal page ID reference rewriting in DynamicWeb content fields
**Researched:** 2026-04-02
**Confidence:** HIGH

## How Other CMS Sync Tools Handle This

### Sitecore Unicorn / Rainbow
- Sitecore stores all references as GUIDs natively -- link fields, rich text `<link>` elements, and General Link fields all use item GUIDs
- Unicorn serializes fields verbatim (GUIDs pass through unchanged between environments)
- After deserialization, Unicorn optionally rebuilds the **Link Database** (`updateLinkDatabase` config flag) which indexes cross-references for the authoring UI
- **No numeric ID rewriting needed** because Sitecore's internal link format is GUID-based
- Lesson: If your storage format is GUID-native, link resolution is a non-problem

### Umbraco uSync
- Umbraco historically stored content picker values as numeric node IDs; migrated to UDI (GUID-based URNs like `umb://document/{guid}`) in v7.6+
- uSync uses **Value Mappers** -- pluggable transformers that run per-field-type during import/export
- `GetExportValue()`: converts local integer IDs to GUID-based UDIs during serialization
- `GetImportValue()`: converts UDIs back to local integer IDs during deserialization
- Rich text fields have a dedicated mapper that parses HTML and remaps `umb://` URIs
- uSync docs explicitly warn: "Never store integer IDs in your property editors" -- use GUID/UDI instead
- Lesson: Per-field-type mapper architecture is powerful but over-engineered for DW's uniform link format

### WordPress Migration Tools
- WordPress stores internal links as absolute/relative URLs with numeric post IDs (`?p=123`)
- Migration tools (Better Search Replace, WP Importer 0.9.5+) do **string-based search-and-replace** across the database
- WordPress Importer 0.9.5 now rewrites URLs in imported content automatically during import
- Pattern: brute-force regex replacement, no field-type awareness
- Lesson: Regex works when the link format is uniform. DW's `Default.aspx?ID=NNN` is just as uniform.

### Common Patterns Across All Tools

1. **Two-phase approach**: Build ID mapping table first, then rewrite references
2. **GUID as bridge**: Every tool uses a stable identifier (GUID/UDI/slug) to bridge source and target numeric IDs
3. **Fail-safe on unmapped**: Leave original reference intact rather than blanking it
4. **Inline rewriting preferred**: Rewrite during deserialization, not as a post-fixup pass

## DynamicWeb Internal Link Formats

All confirmed via DW10 API docs and LinkHelper source.

### Format 1: Simple Page Link
```
Default.aspx?ID=123
```
**Where found:** LinkEditor fields, ButtonEditor fields, rich text `<a href="">` attributes
**Frequency:** Most common. Every internal page link uses this format.
**Resolution:** Replace `123` with target page ID using GUID bridge.

### Format 2: Page Link with Paragraph Anchor
```
Default.aspx?ID=123#456
```
**Where found:** LinkEditor/ButtonEditor fields when linking to a specific paragraph
**Frequency:** Less common but does occur in Swift templates.
**Resolution:** Replace both `123` (page ID) and `456` (paragraph ID) with target IDs.

### Format 3: Page Link with Additional Query Parameters
```
Default.aspx?ID=123&GroupID=GROUP1
Default.aspx?ID=123&ProductID=PROD1
```
**Where found:** Ecommerce product/group page links
**Frequency:** Common in ecommerce solutions. The `ID=` part is the page ID; other params are ecommerce identifiers.
**Resolution:** Only rewrite the `ID=123` part. Leave `GroupID`, `ProductID` untouched.

### Format 4: Rich Text HTML with Embedded Links
```html
<a href="Default.aspx?ID=123">Click here</a>
<p>Visit <a href="Default.aspx?ID=456#789">this section</a> for details.</p>
```
**Where found:** HTML/rich text fields on paragraphs (e.g., `Text` field, custom HTML fields)
**Frequency:** Common in content-heavy pages.
**Resolution:** Same regex as Format 1/2 -- the pattern is unambiguous in HTML context.

### Format 5: Product/Group-Only Links (DO NOT REWRITE)
```
ProductID=PROD123
GroupID=GROUP456
```
**Where found:** Product catalog link fields
**Detection:** No `Default.aspx?ID=` prefix -- these are NOT page references
**Resolution:** SKIP. Product/group IDs are string-based, not numeric page references.

### Format 6: File Links (DO NOT REWRITE)
```
/Files/images/banner.jpg
```
**Where found:** Image and file picker fields
**Resolution:** SKIP. File paths are environment-independent (files live in git).

## Table Stakes

Features that MUST work for link resolution to be useful. Missing = broken links between environments, which defeats the purpose of content sync.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Source-to-target ID mapping via GUID bridge | Core mechanism -- every sync tool does this. Without it, no resolution possible. | Low | Add `SourcePageId` to SerializedPage; build mapping during deserialization |
| Page ID rewriting in ItemType fields | Most common internal link location. Every `Default.aspx?ID=NNN` in field values must be rewritten. | Low | `Regex.Replace` with match evaluator in `SaveItemFields` |
| Page ID rewriting in rich text/HTML fields | Content editors embed links in HTML; same `Default.aspx?ID=NNN` pattern | Low | Same regex works in HTML context -- no HTML parser needed |
| Page ID rewriting in paragraph Text field | `paragraph.Text` can contain rich text with embedded links | Low | Apply same logic to Text field during paragraph deserialization |
| Multi-reference field support | Fields may contain multiple `Default.aspx?ID=NNN` references | Low | Global regex replace (not single match) handles this automatically |
| Skip non-page references | Must not corrupt external URLs, file paths, or ecommerce IDs | Low | Regex only matches `Default.aspx?ID=(\d+)` -- other formats untouched |
| Query-string parameter preservation | `Default.aspx?ID=123&GroupID=456` -- rewrite ID, preserve rest | Low | Regex captures only the ID portion; rest of string unchanged |
| Unresolvable link warning | Source page ID with no GUID match -- log warning, leave unchanged | Low | Critical for debugging; never silently corrupt data |
| Leave unmapped references intact | Destructive blanking is worse than a broken link | Low | Fail-safe: if no mapping exists, preserve original value |

## Differentiators

Nice-to-have features that improve quality.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Paragraph anchor resolution (`#ParagraphID`) | Complete link fidelity for fragment links | Medium | Requires paragraph GUID mapping (partially exists via `ReferenceResolver`) |
| Dry-run link resolution reporting | Show what WOULD be rewritten without writing | Low | Extend existing dry-run diff logging |
| Link resolution statistics in result | "Resolved 47 internal links across 23 fields" | Low | Add counters to `DeserializeResult` |
| GUID-native YAML storage (long-term) | Eliminate rewriting entirely; matches Sitecore/uSync best practice | Medium | Replace `Default.aspx?ID=42` with GUID-based references during serialization. Breaking format change -- defer to v0.4+ |

## Anti-Features

Features to explicitly NOT build.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| GUID-native YAML storage in v0.3.1 | Breaking change to serialized format; existing YAML files would need migration | Defer to v0.4+. Rewrite during deserialization for now. |
| HTML DOM parsing for link detection | Overkill; `Default.aspx?ID=NNN` is unambiguous in any text context | Single regex covers all contexts |
| Field-type-aware mapper system (uSync-style) | Over-engineered for DW's uniform link format | One regex pattern covers all field types |
| Bi-directional link resolution | Only source-wins model exists; no merge scenario | Only build source-to-target direction |
| Cross-area link resolution | Links between different Areas/websites | Out of scope -- each predicate processes one area. Defer until real-world need. |
| Friendly URL resolution | Friendly URLs are computed at render time, not stored | DW handles this automatically from correct page IDs |
| Link validation (check target page exists) | Target page may be deserialized later in the same run | Log warning for unresolvable IDs, but do not block |
| Media/file path rewriting | Files are in git, paths are relative and stable | Files are out of scope per PROJECT.md |
| Ecommerce product/group ID rewriting | Different ID systems, not numeric page IDs | Skip -- these are string-based identifiers, not page references |

## Feature Dependencies

```
Source ID Serialization --> ID Map Building --> Link Detection --> Link Rewriting
      (serialize)           (pre-deserialize)   (during deserialize)     |
                                                                Paragraph Anchor
                                                                Resolution (optional)
```

Complete dependency chain:
1. **Serialize**: Add `SourcePageId` to `SerializedPage` YAML (one field on DTO, one line in mapper)
2. **Pre-deserialize**: Scan all YAML pages to build `{SourcePageId -> PageUniqueId}` mapping
3. **During deserialize**: Combine with existing `PageGuidCache` (GUID -> targetID) to get full `{sourceID -> targetID}` mapping
4. **Apply**: Regex-replace `Default.aspx?ID=NNN` in field values using the mapping

## Mapping Table Construction: The Core Design Challenge

**Problem**: The YAML currently stores `PageUniqueId` (GUID) per page but NOT the source numeric page ID. Without knowing what numeric ID each page had in the source, we cannot map `Default.aspx?ID=42` to anything.

### Options Evaluated

| Option | How It Works | Pros | Cons | Verdict |
|--------|-------------|------|------|---------|
| **(A) Add `SourcePageId` to SerializedPage** | `ContentMapper.MapPage` already has `page.ID`; add it to the DTO | Clean, self-contained, backward-compatible | Minor YAML format addition | **Recommended** |
| (B) Separate mapping file | Persist `ReferenceResolver._pageGuidCache` as a standalone `_id-map.yml` | No DTO changes | Extra file to manage, can desync | Rejected |
| (C) Infer from field values | Scan fields for IDs, try to match against YAML pages | No format changes | Cannot infer which GUID maps to which numeric ID | **Impossible** |
| (D) GUID-native storage | Replace IDs with GUIDs during serialization | Eliminates the problem | Breaking format change | Deferred to v0.4+ |

**Decision: Option A** -- Add `SourcePageId` to `SerializedPage`. Backward-compatible (old YAML without it simply skips link resolution), minimal change, self-contained, complete.

## MVP Recommendation

Prioritize (in build order):

1. **Add `SourcePageId` to `SerializedPage`** -- One field on record type, one line in `ContentMapper.MapPage` (already has `page.ID`). Requires re-serialization. (30 min)
2. **Build sourceID-to-GUID mapping in ContentDeserializer** -- Pre-scan YAML tree before deserialization loop. Dictionary from `SourcePageId -> PageUniqueId`. (30 min)
3. **Compose full sourceID-to-targetID mapping** -- Combine with existing `WriteContext.PageGuidCache`. (15 min)
4. **Regex rewriting in `SaveItemFields`** -- `Regex.Replace` with match evaluator on all field values. Pattern: `Default\.aspx\?ID=(\d+)`. (1 hour)
5. **Same rewriting for paragraph `Text` field** -- Apply in paragraph deserialization path. (30 min)
6. **Skip guards and logging** -- Log resolved links, warn on unmapped, leave originals intact. (30 min)
7. **Dry-run link report** -- Extend existing dry-run to show link rewrites. (30 min)

Defer:
- Paragraph anchor resolution (`#ParagraphID`): Less common, adds paragraph GUID mapping complexity
- GUID-native YAML storage: Valuable long-term, breaking change -- defer to v0.4+
- Cross-area links: Out of scope until real-world need surfaces
- Link resolution summary counts: Polish after core rewriting works

## Complexity Assessment

| Feature | Effort | Risk | Notes |
|---------|--------|------|-------|
| `SourcePageId` on SerializedPage | 30 min | Low | One field on record, one line in mapper |
| Source-to-target mapping table | 45 min | Low | Dictionary from pre-scan of YAML tree |
| Regex detection + rewriting | 1 hour | Low | Well-defined pattern, `Regex.Replace` with evaluator |
| Paragraph Text rewriting | 30 min | Low | Same logic, different injection point |
| Skip guards + logging | 30 min | Low | Regex only matches page ID pattern |
| Dry-run link report | 30 min | Low | Extend existing dry-run |
| **Total MVP** | **~3.5 hours** | **Low** | All features use existing infrastructure |

## Sources

- [Sitecore Unicorn GitHub](https://github.com/SitecoreUnicorn/Unicorn) -- sync config with `updateLinkDatabase` flag (MEDIUM confidence)
- [Rainbow serialization engine](https://github.com/SitecoreUnicorn/Rainbow) -- GUID-native field storage approach (MEDIUM confidence)
- [uSync Value Mappers docs](https://docs.jumoo.co.uk/usync/13.x/uSync/extending/valuemappers/) -- Export/Import value transformation, GetExportValue/GetImportValue pattern (HIGH confidence)
- [uSync GUID-based sync discussion](https://our.umbraco.com/packages/developer-tools/usync/usync/107717-can-usync-complete-be-set-to-use-uuid-instead-of-id-when-publishing) -- UDI/GUID as cross-environment bridge (MEDIUM confidence)
- [DynamicWeb Default.aspx?ID format](https://doc.dynamicweb.com/forum/cms-standard-features/avoid-default-aspx-urls) -- Canonical internal link format confirmed (HIGH confidence)
- [DynamicWeb LinkHelper API](https://doc.dynamicweb.dev/api/Dynamicweb.Environment.Helpers.LinkHelper.html) -- Confirms `Default.aspx?ID=pageId` as standard format (HIGH confidence)
- [WordPress Importer URL migration](https://make.wordpress.org/core/2025/11/27/wordpress-importer-can-now-migrate-urls-in-your-content/) -- String-based URL rewriting approach (MEDIUM confidence)
- Codebase: `ReferenceResolver.cs` -- existing `_pageGuidCache` with `ResolvePageGuid` method (HIGH confidence)
- Codebase: `ContentMapper.cs` -- `MapPage` has `page.ID`, `ExtractItemFields` returns opaque strings (HIGH confidence)
- Codebase: `ContentDeserializer.cs` -- `WriteContext.PageGuidCache` provides GUID-to-targetID, `SaveItemFields` is rewriting injection point (HIGH confidence)

---
*Feature research for: DynamicWeb.Serializer v0.3.1 -- Internal Link Resolution*
*Researched: 2026-04-02*
