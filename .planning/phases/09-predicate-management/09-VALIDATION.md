---
phase: 9
slug: predicate-management
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-22
---

# Phase 9 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (existing) |
| **Config file** | tests/Dynamicweb.ContentSync.Tests/Dynamicweb.ContentSync.Tests.csproj |
| **Quick run command** | `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "FullyQualifiedName~Predicate" --no-restore -v q` |
| **Full suite command** | `dotnet test tests/Dynamicweb.ContentSync.Tests --no-restore -v q` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "FullyQualifiedName~Predicate" --no-restore -v q`
- **After every plan wave:** Run `dotnet test tests/Dynamicweb.ContentSync.Tests --no-restore -v q`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 09-01-01 | 01 | 1 | PRED-01, PRED-02 | unit + build | `dotnet build && dotnet test --filter Predicate` | ❌ W0 | ⬜ pending |
| 09-01-02 | 01 | 1 | PRED-03, PRED-04, PRED-05, PRED-06 | unit + build | `dotnet build && dotnet test --filter Predicate` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Dynamicweb.ContentSync.Tests/AdminUI/PredicateCommandTests.cs` — stubs for save/delete commands
- [ ] Existing test infrastructure covers ConfigLoader/ConfigWriter round-trips

*Existing infrastructure covers most phase requirements. Only command-level tests need creation.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Predicates sub-node visible in admin tree | PRED-01 | DW admin UI rendering | Navigate Settings > Content > Sync, verify Predicates node appears |
| Content tree picker opens and selects page | PRED-03 | UI interaction | Click Path field, verify tree picker opens with area-scoped pages |
| Delete confirmation dialog appears | PRED-05 | UI interaction | Right-click predicate, click Delete, verify confirmation prompt |
| Predicate changes respected by scheduled task | PRED-06 | End-to-end | Add predicate via UI, run serialize task, verify only matching content serialized |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
