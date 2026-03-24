# Phase 18: Predicate Config Multi-Provider Support - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-03-24
**Phase:** 18-predicate-config-multi-provider
**Areas discussed:** Provider type switching UX, SqlTable field presentation, List screen display, New predicate defaults

---

## Provider Type Switching UX

### Question 1: How should the ProviderType field work?

| Option | Description | Selected |
|--------|-------------|----------|
| Dropdown with reload (Recommended) | Select dropdown with WithReloadOnChange() — selecting Content shows Area/Page/Excludes, selecting SqlTable shows Table/NameColumn/CompareColumns/ServiceCaches | ✓ |
| Two separate edit screens | Separate ContentPredicateEditScreen and SqlTablePredicateEditScreen, navigated from list based on type | |
| You decide | Claude picks the best approach based on codebase patterns | |

**User's choice:** Dropdown with reload
**Notes:** Same pattern as existing AreaId selector

### Question 2: What happens when switching type on existing predicate?

| Option | Description | Selected |
|--------|-------------|----------|
| Allow switching, clear old fields | Changing type clears the previous type's fields | |
| Lock type after creation | ProviderType only editable on new predicates. Delete and recreate to change. | ✓ |
| Allow switching, keep old fields | Switching type shows new fields but silently preserves old values | |

**User's choice:** Lock type after creation

---

## SqlTable Field Presentation

### Question 3: How should SqlTable fields be presented?

| Option | Description | Selected |
|--------|-------------|----------|
| All free text inputs | Simple text fields for all. User must know exact values. | |
| Table as dropdown, rest as text | Table populated from known tables. Others as free text. | ✓ |
| You decide | Claude picks based on what's feasible | |

**User's choice:** Table as dropdown, rest as text
**Notes:** User noted DataGroups are just a temp reference and don't exist in all DW environments

### Question 4: Where should the Table dropdown get its values?

| Option | Description | Selected |
|--------|-------------|----------|
| Free text with DataGroup hints (Recommended) | Text input with optional DataGroup XML hints. Auto-fill when match found. | ✓ |
| Query sys.tables | Populate from SQL Server. Always available but shows ALL tables. | |
| Pure free text, no hints | All fields are simple text inputs. | |

**User's choice:** Free text with DataGroup hints
**Notes:** Research done on DW Schema Management docs and DataGroup API. XmlDataGroupRepository is internal but XMLs can be parsed directly.

---

## List Screen Display

### Question 5: How to differentiate Content vs SqlTable in list?

| Option | Description | Selected |
|--------|-------------|----------|
| Add Type column + generic Target column | Replace Path with Target (Path for Content, Table for SqlTable) | |
| Add Type column, keep existing columns | SqlTable predicates show blank for Path/Area | |
| You decide | Claude picks best layout | ✓ |

**User's choice:** You decide

---

## New Predicate Defaults

### Question 6: Default ProviderType for new predicates?

| Option | Description | Selected |
|--------|-------------|----------|
| Default to Content | Backward compatible. User switches to SqlTable if needed. | |
| No default - must choose first | Form empty/disabled until user picks type. Forces explicit choice. | ✓ |
| You decide | Claude picks sensible default | |

**User's choice:** No default - must choose first

---

## Claude's Discretion

- List screen column layout
- DataGroup XML parsing approach
- SqlTable field validation rules
- Form field disable behavior when no ProviderType selected

## Deferred Ideas

None
