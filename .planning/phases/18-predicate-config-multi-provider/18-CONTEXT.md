# Phase 18: Predicate Config Multi-Provider Support - Context

**Gathered:** 2026-03-24
**Status:** Ready for planning

<domain>
## Phase Boundary

Extend the existing predicate admin UI (edit screen, list screen, save command, queries) to support both Content and SqlTable provider types. The edit screen shows provider-specific field groups based on a ProviderType dropdown. The list screen differentiates predicate types visually. All changes persist to the same config file via ConfigWriter/ConfigLoader using the existing ProviderPredicateDefinition model.

</domain>

<decisions>
## Implementation Decisions

### Provider Type Switching UX
- **D-01:** ProviderType is a Select dropdown with `WithReloadOnChange()` at the top of the edit form. Selecting Content shows Area/Page/Excludes fields. Selecting SqlTable shows Table/NameColumn/CompareColumns/ServiceCaches fields.
- **D-02:** ProviderType is locked after creation — only editable on new predicates. To change type, delete and recreate the predicate.
- **D-03:** The existing `WithReloadOnChange()` pattern on the AreaId selector (PredicateEditScreen line 29-32) is the reference implementation.

### SqlTable Field Presentation
- **D-04:** Table is a free-text input with DataGroup hints — if DataGroup XML files exist on disk (`Files/System/Deployment/DataGroups/`), show a helper dropdown/autocomplete populated from them. If XMLs are absent, fall back to pure free text. DataGroups are just a reference; they don't exist in all DW environments.
- **D-05:** When a DataGroup match is found for the entered Table name, auto-fill NameColumn, CompareColumns, and ServiceCaches from the DataGroup metadata.
- **D-06:** NameColumn and CompareColumns are simple text inputs.
- **D-07:** ServiceCaches is a multi-line textarea (same pattern as Excludes field).

### List Screen Display
- **D-08:** Claude's Discretion — choose the best list column layout to differentiate Content vs SqlTable predicates. Options considered: Type+Target columns, or Type column with existing Path/Area staying as-is.

### New Predicate Defaults
- **D-09:** New predicates have no default ProviderType — the form fields are empty/disabled until user picks Content or SqlTable. Forces explicit choice.

### Claude's Discretion
- List screen column layout (D-08)
- Exact DataGroup XML parsing approach (reuse existing code or new helper)
- Validation rules for SqlTable fields (which are required vs optional)
- How to disable/hide form fields when no ProviderType is selected

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Current Predicate Admin UI (files to modify)
- `src/DynamicWeb.Serializer/AdminUI/Models/PredicateEditModel.cs` — Current edit model, Content-only fields
- `src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs` — Edit screen with AreaId WithReloadOnChange() pattern
- `src/DynamicWeb.Serializer/AdminUI/Commands/SavePredicateCommand.cs` — Save command, hardcodes ProviderType="Content" at line 70
- `src/DynamicWeb.Serializer/AdminUI/Queries/PredicateByIndexQuery.cs` — Load query, only maps Content fields
- `src/DynamicWeb.Serializer/AdminUI/Queries/PredicateListQuery.cs` — List query, maps Content-specific display
- `src/DynamicWeb.Serializer/AdminUI/Models/PredicateListModel.cs` — List model, Content-only columns
- `src/DynamicWeb.Serializer/AdminUI/Screens/PredicateListScreen.cs` — List screen with RowViewMapping

### Data Model (source of truth for all provider fields)
- `src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs` — Full model with Content fields (AreaId, Path, PageId, Excludes) and SqlTable fields (Table, NameColumn, CompareColumns, ServiceCaches)

### DW DataGroup API (for Table field hints)
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Deployment\DataGroup.cs` — DataGroup model with DataItemTypes and ServiceCaches
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Deployment\DataGroupRepository.cs` — Public abstract GetAll()/GetById()
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Deployment\XmlDataGroupRepository.cs` — Internal concrete impl, reads from Files/System/Deployment/DataGroups/
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Deployment\DataItemType.cs` — Table metadata (Id, Name, ProviderParameters)

### Debug Investigation (root cause analysis)
- `.planning/debug/predicate-config-missing-providers.md` — Full diagnosis of what's missing in the current UI

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **PredicateEditScreen.GetEditor()**: Already uses `WithReloadOnChange()` on AreaId selector — same pattern for ProviderType
- **Textarea editor**: Already used for Excludes field — reuse for ServiceCaches
- **SelectorBuilder.CreateAreaSelector/CreatePageSelector**: Content-specific selectors already wired
- **ConfigWriter/ConfigLoader**: Already round-trips ProviderPredicateDefinition with all fields
- **ProviderPredicateDefinition**: Already has all fields for both providers — no model changes needed

### Established Patterns
- `EditScreenBase<T>` with `BuildEditScreen()` adding component groups
- `GetEditor()` override for custom editor types per property
- `WithReloadOnChange()` triggers form postback on dropdown change
- Index-based predicate identity (1-based for DW framework, 0-based internally)
- `CommandBase<T>` for save with validation

### Integration Points
- PredicateEditModel needs new properties (ProviderType, Table, NameColumn, CompareColumns, ServiceCaches)
- PredicateByIndexQuery needs to map all ProviderPredicateDefinition fields
- SavePredicateCommand needs to read ProviderType from model instead of hardcoding "Content"
- PredicateListQuery/PredicateListModel need type-aware display

</code_context>

<specifics>
## Specific Ideas

- DW Schema Management (https://doc.dynamicweb.dev/manual/dynamicweb10/settings/areas/integration/schemamanagement.html) was considered but is about integration schemas, not SQL table enumeration
- DataGroup XMLs at `Files/System/Deployment/DataGroups/` are the best source for table hints, but XmlDataGroupRepository is `internal` — need to parse XMLs directly or use reflection
- The existing codebase already parses DataGroup XMLs via `System.Xml.Linq` in SqlTableProvider (Phase 13 pattern)

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 18-predicate-config-multi-provider*
*Context gathered: 2026-03-24*
