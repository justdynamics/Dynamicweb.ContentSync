---
phase: 04
slug: deserialization
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-19
---

# Phase 04 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.x with Dynamicweb.ContentSync.Tests and IntegrationTests |
| **Config file** | `tests/Dynamicweb.ContentSync.Tests/Dynamicweb.ContentSync.Tests.csproj` |
| **Quick run command** | `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "Category=Deserialization"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~15 seconds (unit), ~60 seconds (integration with live DW) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "Category=Deserialization"`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 04-01-01 | 01 | 1 | DES-02 | unit | `dotnet test --filter "IdentityResolution"` | ❌ W0 | ⬜ pending |
| 04-01-02 | 01 | 1 | DES-03 | unit | `dotnet test --filter "DependencyOrder"` | ❌ W0 | ⬜ pending |
| 04-02-01 | 02 | 1 | DES-01 | unit | `dotnet test --filter "ContentDeserializer"` | ❌ W0 | ⬜ pending |
| 04-02-02 | 02 | 1 | DES-04 | unit | `dotnet test --filter "DryRun"` | ❌ W0 | ⬜ pending |
| 04-03-01 | 03 | 2 | DES-01 | integration | `dotnet test tests/Dynamicweb.ContentSync.IntegrationTests --filter "Deserialization"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Dynamicweb.ContentSync.Tests/Deserialization/` — test directory for deserialization unit tests
- [ ] Integration test stubs in `tests/Dynamicweb.ContentSync.IntegrationTests/Deserialization/`
- [ ] Existing xunit infrastructure and ContentTreeBuilder fixture cover shared needs

*Existing infrastructure covers shared fixtures; deserialization-specific test files created in Wave 0.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live DW write verification | DES-01 | Requires running Swift2.1 instance | Deploy DLL, run deserialize task, verify pages in DW admin |
| GUID preservation on insert | DES-02 | Must verify DW doesn't overwrite GUID | Insert new page, query DB, confirm UniqueId matches YAML |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
