# Domain Pitfalls: Internal Link Resolution

**Domain:** Internal page ID rewriting during content deserialization
**Researched:** 2026-04-02
**Confidence:** HIGH for critical/moderate pitfalls; MEDIUM for minor (some need runtime validation)

## Critical Pitfalls

Mistakes that cause data corruption or broken links.

### Pitfall 1: Forward Reference Problem (Single-Pass Resolution)
**What goes wrong:** Resolving links during page deserialization (inline with `SaveItemFields`) fails because target pages may not exist yet. Page A at sort=1 links to Page C at sort=3, but C hasn't been deserialized when A's fields are saved.
**Why it happens:** The deserializer processes the page tree depth-first. Sibling and cross-branch references are common.
**Consequences:** Links to not-yet-deserialized pages would be logged as "unresolvable" and left with source IDs (broken links).
**Prevention:** Two-phase approach. Phase 1: deserialize ALL pages. Phase 2: resolve ALL links. Never resolve inline.
**Detection:** Test with a content tree where child pages link to sibling pages.

### Pitfall 2: ID Collision in String Replacement
**What goes wrong:** `string.Replace("Default.aspx?ID=1", "Default.aspx?ID=999")` also replaces inside `Default.aspx?ID=12` and `Default.aspx?ID=100`.
**Why it happens:** Naive string replacement without word boundary consideration. Page ID `1` is a substring of `12`, `100`, `1234`, etc.
**Consequences:** Corrupted links pointing to wrong pages. Very hard to debug.
**Prevention:** Process IDs in descending order of string length (longest first), OR use precise replacement that ensures the ID is followed by a non-digit character (end of string, `&`, `#`, `"`, `'`, `<`, space).
**Detection:** Unit test with IDs 1, 12, 123 all present in the same field value.

### Pitfall 3: Rewriting Non-Page ID Parameters
**What goes wrong:** A URL like `Default.aspx?ID=123&AreaID=2` or `Default.aspx?ID=123&ParagraphID=456` has the `ID=123` rewritten but other ID-bearing parameters are mistakenly also rewritten.
**Why it happens:** Over-aggressive regex or string replacement that matches `ID=\d+` anywhere in the URL.
**Consequences:** Corrupted query parameters, broken ecommerce or area references.
**Prevention:** Use `LinkHelper.GetInternalPageIdsFromText` which specifically extracts the `ID=` page parameter, not other parameters. For replacement, only replace the exact `Default.aspx?ID=NNN` prefix, not arbitrary `ID=NNN` occurrences.
**Detection:** Unit test with URLs containing `AreaID=`, `GroupID=`, `ParagraphID=` parameters.

### Pitfall 4: Missing Source Page ID in YAML (Deserialization Without ID Map)
**What goes wrong:** Existing YAML files serialized before this feature don't have `SourcePageId`. The resolver has no source-to-target mapping for these files.
**Why it happens:** Backward compatibility -- old YAML lacks the new field.
**Consequences:** Link resolution silently does nothing for old YAML files. Links remain broken.
**Prevention:** Make link resolution gracefully degrade when `SourcePageId` is missing (log warning, skip resolution). Document that re-serialization is needed after upgrading to pick up source IDs.
**Detection:** Deserialize YAML files that lack `SourcePageId` field -- should warn, not crash.

## Moderate Pitfalls

### Pitfall 5: Paragraph Anchor IDs Not Resolved
**What goes wrong:** `Default.aspx?ID=123#456` has the page ID rewritten but the paragraph ID `456` stays as the source value.
**Why it happens:** Paragraph IDs are numeric and environment-specific, just like page IDs, but they're in the URL fragment (after `#`).
**Consequences:** Link goes to the correct page but wrong paragraph anchor. User doesn't land on expected section.
**Prevention:** Phase 4 extension: build paragraph source-to-target map (similar to page map) and rewrite fragment IDs. For MVP, log a warning when paragraph anchors are detected but not resolved.

### Pitfall 6: Case Sensitivity in URL Matching
**What goes wrong:** DW might store links as `default.aspx?id=123` (lowercase) in some contexts, while `LinkHelper.GetInternalPageIdsFromText` might expect a specific case.
**Why it happens:** URL case is not standardized across all content editors and import sources.
**Consequences:** Links with non-standard casing are missed by the resolver.
**Prevention:** Verify `LinkHelper.GetInternalPageIdsFromText` handles case-insensitive matching (it likely does since DW's own code uses it). Add unit test with mixed-case URLs.

### Pitfall 7: Double Resolution on Re-Deserialization
**What goes wrong:** Running deserialization twice. First run resolves `ID=123` to `ID=456`. Second run sees `ID=456` and tries to resolve it again -- but 456 is a TARGET ID, not a SOURCE ID.
**Why it happens:** The resolver uses the source-to-target map. On second run, the source-to-target map is rebuilt from the same YAML (same source IDs), and 456 is not in the source map, so it's left alone. This is actually SAFE.
**Consequences:** None -- the second run simply doesn't find 456 in the source map and leaves it unchanged. But the field value now has the target ID, which is correct.
**Prevention:** This is safe by design because the source-to-target map only contains IDs from the YAML's `SourcePageId` values, not from the target DB. However, log clearly so operators understand what happened.

## Minor Pitfalls

### Pitfall 8: Performance on Large Content Trees
**What goes wrong:** Phase 2 re-reads ALL Item fields for ALL deserialized pages to scan for links. On a site with 10,000 pages, this adds significant I/O.
**Prevention:** Optimization: during Phase 1, track which items had string fields (most items do). Could also track which specific field values contained `Default.aspx?ID=` during serialization and mark them. For MVP, accept the O(n) re-read -- correctness over performance.

### Pitfall 9: ButtonEditor Serialized Format Unknown
**What goes wrong:** ButtonEditor stores its value as a serialized string (possibly JSON). The exact format may wrap the URL in a way that `GetInternalPageIdsFromText` doesn't find.
**Prevention:** Test with actual ButtonEditor field values from Swift 2.2. If the format is JSON-wrapped, may need to deserialize the JSON, resolve links in individual properties, and re-serialize. LOW confidence -- needs runtime validation.

### Pitfall 10: PropertyItem Fields May Also Contain Links
**What goes wrong:** PropertyItem fields (like page properties) could theoretically contain link values that need resolution.
**Prevention:** Scan PropertyItem fields the same way as regular Item fields. The overhead is minimal.

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| Serialize source IDs | YAML backward compatibility (Pitfall 4) | Make SourcePageId optional with graceful fallback |
| Link detection | ID collision in replacement (Pitfall 2) | Descending length sort OR boundary-aware replacement |
| Pipeline integration | Forward references (Pitfall 1) | Strict two-phase architecture |
| Paragraph anchors | Fragment IDs not resolved (Pitfall 5) | Log warning in MVP, resolve in Phase 4 |
| Re-deserialization | Double resolution concern (Pitfall 7) | Safe by design -- document for operators |

## Sources

- Direct analysis of `ContentDeserializer.cs` deserialization flow
- [LinkHelper API](https://doc.dynamicweb.dev/api/Dynamicweb.Environment.Helpers.LinkHelper.html) -- method behavior
- [DW Customized URLs](https://doc.dynamicweb.com/documentation-9/platform/platform-tools/customized-urls) -- URL format specification
- String replacement collision patterns -- standard software engineering concern
