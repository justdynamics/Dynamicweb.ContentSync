---
phase: 3
slug: serialization
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-19
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.3 |
| **Config file** | tests/Dynamicweb.ContentSync.Tests/Dynamicweb.ContentSync.Tests.csproj |
| **Quick run command** | `dotnet test --filter "Category=Unit"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "Category=Unit"`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| TBD | TBD | TBD | SER-03 | integration | `dotnet test` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | INF-02 | unit | `dotnet test --filter "Category=Unit"` | ✅ (Phase 1) | ⬜ pending |
| TBD | TBD | TBD | INF-03 | unit | `dotnet test --filter "Category=Unit"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] ContentSerializer unit tests with mocked DW services for mapping logic
- [ ] Long-path handling test stubs for INF-03
- [ ] Integration test against live Swift2.2 instance for SER-03

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Customer Center YAML matches DW admin content | SER-03 | Requires visual comparison of DW admin vs YAML files | Spot-check 3 pages in DW admin, compare to YAML |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
