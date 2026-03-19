# Dynamicweb.ContentSync

## What This Is

A DynamicWeb AppStore app that serializes and deserializes content trees to/from YAML files on disk, enabling content to be version-controlled and deployed alongside code. It's the DynamicWeb equivalent of Sitecore Unicorn — replacing the manual sync process of DynamicWeb's built-in deployment tool with an automated, developer-friendly content synchronization workflow.

## Core Value

Developers can reliably move content between DynamicWeb environments through source control, with serialized YAML files as the single source of truth.

## Requirements

### Validated

<!-- Shipped and confirmed valuable. -->

- [x] Serialize full content trees (Area > Pages > Grids > Rows > Paragraphs) to YAML files on disk — Validated in Phase 1-3
- [x] Mirror-tree file layout: folder structure reflects content hierarchy with .yml files per item — Validated in Phase 1
- [x] Unicorn-style predicate configuration to define which content trees to include/exclude — Validated in Phase 2
- [x] PageUniqueId (GUID) as canonical identity — match on GUID, not numeric ID — Validated in Phase 3-4
- [x] Source-wins conflict strategy: serialized files always overwrite target DB on deserialize — Validated in Phase 3-4
- [x] Deserialize YAML files back into a DynamicWeb database — Validated in Phase 4
- [x] New numeric IDs assigned on deserialize when GUID doesn't exist in target — Validated in Phase 4
- [x] Configurable via standalone config file (not DynamicWeb admin UI) — Validated in Phase 2

### Active

<!-- Current scope. Building toward these. -->

- [ ] Two scheduled tasks: one for full serialization, one for full deserialization
- [ ] Structured as a DynamicWeb AppStore app (NuGet package with `dynamicweb-app-store` tag)
- [ ] Comprehensive error handling and logging
- [ ] Tested across multiple content trees beyond the initial test case

### Out of Scope

- Real-time change detection via Notifications API — v2 feature
- DynamicWeb admin UI for configuration — v1 uses config file only
- Publishing to NuGet — final stages only
- Media/file serialization (images, documents) — content structure only for v1
- Partial/incremental sync — v1 does full sync only
- OAuth/licensing — open source app

## Context

**DynamicWeb Content Hierarchy:**
- Website (Area) → Pages → Grid → Rows → Columns → Paragraphs
- Pages use numeric IDs as primary keys but also have PageUniqueId (GUID)
- Paragraphs and other content items follow similar ID patterns
- Item Types extend pages and paragraphs with custom fields

**Inspiration — Sitecore Unicorn:**
- Unicorn uses a predicate system (SerializationPresetPredicate) to define which content trees to serialize
- Predicates support include/exclude rules with path-based matching
- ContentSync will adapt this pattern for DynamicWeb's content model

**DynamicWeb AppStore App Structure:**
- .NET 8.0+ class library
- Required NuGet tags: `dynamicweb-app-store`, `task`
- Scheduled tasks extend `BaseScheduledTaskAddIn` with `[AddInName]`, `[AddInLabel]`, `[AddInDescription]` attributes
- Notifications API available for future change detection (Standard.Page, Standard.Paragraph, ItemNotification)

**Test Environment:**
- Two pre-configured DynamicWeb instances at `C:\Projects\Solutions\swift.test.forsync`
- Test case: serialize "Customer Center" content tree (pageid=8385) from instance A, deserialize into instance B
- Both instances can be started with `dotnet run`

**ID Strategy:**
- PageUniqueId (GUID) is the canonical identifier across environments
- Numeric IDs are environment-specific and will differ between instances
- On deserialize: GUID match → update existing item; no GUID match → insert with new numeric ID
- This prevents ID collisions while maintaining content identity

**DynamicWeb Deployment Tool (what we're replacing):**
- Built-in tool only supports manual sync via admin UI
- No source control integration
- No automation via CI/CD pipelines
- ContentSync fills this gap

## Constraints

- **Tech stack**: .NET 8.0+, DynamicWeb 10.2+ APIs — must be a valid AppStore app
- **Serialization format**: YAML — chosen for readability and git-friendly diffs
- **Config approach**: Standalone config file for v1, not DynamicWeb admin UI
- **Sync model**: Full sync only for v1 (no incremental/delta sync)
- **Conflict resolution**: Source (files) always wins — no merge logic in v1

## Key Decisions

<!-- Decisions that constrain future work. Add throughout project lifecycle. -->

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| YAML over JSON/XML | More readable, cleaner git diffs, less verbose than XML | — Pending |
| GUID as canonical identity | Numeric IDs differ between environments; GUIDs are stable | — Pending |
| Source-wins conflict strategy | Simplest model, matches Unicorn default, files are truth | — Pending |
| Mirror-tree file layout | Intuitive mapping between disk and content tree, easy to navigate | — Pending |
| Config file over admin UI | Faster to implement, config lives in source control alongside content | — Pending |
| Full sync via scheduled tasks | Simpler than incremental, notifications deferred to v2 | — Pending |
| Two separate scheduled tasks | Separation of concerns: serialize and deserialize are distinct operations | — Pending |

---
*Last updated: 2026-03-19 after Phase 4 completion*
