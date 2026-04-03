# Research Summary: Internal Link Resolution for DynamicWeb.Serializer

**Domain:** Internal page ID reference rewriting during content deserialization
**Researched:** 2026-04-02
**Overall confidence:** HIGH

## Executive Summary

DynamicWeb stores internal page links as `Default.aspx?ID=NNN` strings in ItemType field values. When content is serialized from one environment and deserialized into another, these numeric page IDs are environment-specific and will point to wrong (or nonexistent) pages. The serializer already resolves structural references (MasterParagraphID, GlobalRecordPageID) via GUIDs, but does NOT yet resolve page ID references embedded in rich text or link field values.

DynamicWeb provides a native API (`LinkHelper.GetInternalPageIdsFromText`) that extracts all internal page IDs from a text string, plus `LinkHelper.IsLinkInternal` and `LinkHelper.GetInternalPageId` for individual URL analysis. This means we do not need custom regex for link detection -- the DW API handles the parsing. We only need to build the source-ID-to-target-ID mapping and perform string replacement.

The link resolution fits naturally into the existing `SaveItemFields` method in `ContentDeserializer.cs`. The `WriteContext.PageGuidCache` already contains the GUID-to-target-ID mapping needed. The serialization side needs to serialize source page IDs alongside their GUIDs so deserialization can build the reverse map.

Three field types contain internal links: **LinkEditor** fields (store `Default.aspx?ID=NNN`), **ButtonEditor** fields (store serialized string with embedded links), and **rich text/HTML fields** (contain `<a href="Default.aspx?ID=NNN">` in HTML content). All three use the same `Default.aspx?ID=NNN` pattern -- just embedded in different contexts.

## Key Findings

**Stack:** No new dependencies. Use existing `Dynamicweb.Environment.Helpers.LinkHelper` API + simple string replacement. Zero NuGet additions.
**Architecture:** Two-pass approach -- first pass deserializes all pages (building complete GUID-to-ID map), second pass rewrites links in field values. OR single-pass with deferred link resolution queue.
**Critical pitfall:** Paragraph anchors (`Default.aspx?ID=NNN#PPP`) contain BOTH page and paragraph IDs that need resolution. Also, link fields may contain product/group references (`ProductID=`, `GroupID=`) that should NOT be rewritten.

## Implications for Roadmap

Based on research, suggested phase structure:

1. **Phase 1: Serialize page ID map** - Add source numeric page IDs to serialized YAML alongside GUIDs
   - Addresses: Building the source-ID-to-target-ID bridge
   - Avoids: Needing to query source DB during deserialization

2. **Phase 2: Link resolver core** - Create `InternalLinkResolver` class with `LinkHelper` integration
   - Addresses: Detection and rewriting of `Default.aspx?ID=NNN` patterns
   - Avoids: Rolling custom regex when DW API exists

3. **Phase 3: Integration into deserialization pipeline** - Wire resolver into `SaveItemFields`
   - Addresses: Actual field value rewriting during deserialization
   - Avoids: Modifying page/paragraph structural properties (only field values)

4. **Phase 4: Paragraph anchor resolution** - Handle `#ParagraphID` fragments
   - Addresses: Complete link fidelity including paragraph anchors
   - Avoids: Breaking simpler page-only links while adding paragraph support

**Phase ordering rationale:**
- Phase 1 must come first because the ID map is needed before any resolution can happen
- Phase 2 is pure logic with no DW integration, easiest to unit test
- Phase 3 wires it together -- depends on phases 1 and 2
- Phase 4 is optional/stretch -- paragraph anchors are less common than page links

**Research flags for phases:**
- Phase 1: Standard -- extending existing YAML model fields
- Phase 2: Needs validation of `LinkHelper.GetInternalPageIdsFromText` behavior with edge cases
- Phase 3: Standard -- follows PermissionMapper integration pattern
- Phase 4: May need deeper research into paragraph GUID resolution during deserialization

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | No new deps, DW API confirmed via official docs |
| Features | HIGH | Link formats well-documented in DW ecosystem |
| Architecture | HIGH | Follows existing patterns in codebase (PermissionMapper, ReferenceResolver) |
| Pitfalls | MEDIUM | Paragraph anchor and ButtonEditor value format need runtime validation |

## Gaps to Address

- ButtonEditor serialized value format not fully documented -- need to inspect actual DB values at runtime
- `LinkHelper.GetInternalPageIdsFromText` behavior with malformed URLs needs testing
- Product/Group link references (`ProductID=`, `GroupID=`) are out of scope but should be explicitly skipped
- Whether link resolution should also apply to PropertyItem fields (Icon, etc.) -- likely no, but verify
