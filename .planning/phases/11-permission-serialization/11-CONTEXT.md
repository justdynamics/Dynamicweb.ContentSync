# Phase 11: Permission Serialization - Context

**Gathered:** 2026-03-22
**Status:** Ready for planning

<domain>
## Phase Boundary

Extend ContentSync serialization to include explicit page permissions in YAML output. Permissions are stored in the `UnifiedPermission` table and consist of role-based (Anonymous, AuthenticatedFrontend, etc.) and user group-based access controls. Only pages with explicit permissions get a permissions section — pages relying on inheritance produce no permission data. Phase 12 handles deserialization and safety fallbacks.

</domain>

<decisions>
## Implementation Decisions

### YAML Structure
- **D-01:** Permissions are inline in page.yml — a `permissions` section at the bottom of each page file (not a separate file)
- **D-02:** Each permission entry has: `owner` (name), `ownerType` ("role" or "group"), `level` (human-readable: none/read/edit/create/delete/all), `levelValue` (numeric: 1/4/20/84/340/1364)
- **D-03:** Both level name and numeric value stored — name is human-readable for git diffs, numeric is authoritative for deserialization
- **D-04:** Pages with no explicit permissions have no `permissions` section in YAML (inheritance preserved by tree structure)

### Owner Identification
- **D-05:** Role owners stored by role name string (e.g. "Anonymous", "AuthenticatedFrontend", "AuthenticatedBackend", "Administrator")
- **D-06:** User group owners stored by group name + source numeric ID (e.g. owner: "Customers", ownerId: "1325")
- **D-07:** ownerType distinguishes roles from groups: "role" for the 4 built-in roles, "group" for user groups
- **D-08:** If a group has been deleted on source (numeric ID in UnifiedPermission but no matching AccessUser record), serialize with raw numeric ID as owner name and ownerType: "group" — deserialize will treat as unresolvable

### Permission Reading
- **D-09:** Read permissions via `PermissionService.GetPermissionsByQuery(new PermissionQuery { Key = pageId.ToString(), Name = "Page" })`
- **D-10:** Resolve group names via `Services.Users` or direct AccessUser lookup by ID — the PermissionUserId for groups is the numeric AccessUser ID as string

### Claude's Discretion
- Exact YAML key naming (camelCase vs snake_case — follow existing YAML conventions in the project)
- How to distinguish role IDs from group IDs (roles are well-known string names, groups are numeric strings)
- Whether to add a helper class for permission mapping or inline in ContentMapper
- PermissionLevel enum-to-string conversion implementation

</decisions>

<specifics>
## Specific Ideas

- The 4 built-in roles have fixed string IDs: "Anonymous", "AuthenticatedBackend", "AuthenticatedFrontend", "Administrator"
- Group IDs are numeric strings (e.g. "1325") that map to AccessUser.AccessUserID where the record is a group (not a user)
- PermissionLevel is a flags enum: None=1, Read=4, Edit=20, Create=84, Delete=340, All=1364
- Example YAML output for Customer Center page:
  ```yaml
  permissions:
    - owner: "Anonymous"
      ownerType: "role"
      level: "none"
      levelValue: 1
    - owner: "AuthenticatedFrontend"
      ownerType: "role"
      level: "read"
      levelValue: 4
  ```

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### DW Permission System
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Security\Permissions\Permission.cs` — Permission record: OwnerId, Identifier (Key+Name+SubName), Level
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Security\Permissions\PermissionService.cs` — GetPermissionsByQuery, SetPermission APIs
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Security\Permissions\PermissionQuery.cs` — Query by Key+Name to get page permissions
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Security\Permissions\PermissionLevel.cs` — Flags enum: None=1, Read=4, Edit=20, Create=84, Delete=340, All=1364
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Security\Permissions\PermissionEntityIdentifier.cs` — Key + Name + SubName identifier
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Security\UserManagement\UserRoles\UserRole.cs` — Built-in roles: Anonymous, AuthenticatedBackend, AuthenticatedFrontend, Administrator
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Security\UserManagement\UserRoles\UserRoleManager.cs` — GetUserRoleById for role lookup

### Existing ContentSync (extend these)
- `src/Dynamicweb.ContentSync/Models/SerializedPage.cs` — Add permissions list
- `src/Dynamicweb.ContentSync/Serialization/ContentMapper.cs` — MapPage method needs permission extraction
- `src/Dynamicweb.ContentSync/Serialization/ContentSerializer.cs` — Orchestrates serialization (no changes expected)

### DB Schema (verified on Swift-2.2)
- Table `UnifiedPermission`: PermissionID, PermissionUserId, PermissionKey, PermissionName, PermissionSubName, PermissionLevel
- Table `AccessUser`: AccessUserID, AccessUserUserName, AccessUserName — groups and users share this table

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ContentMapper.MapPage()`: Already maps pages to SerializedPage DTOs — extend with permissions
- `ContentMapper.ExtractItemFields()`: Pattern for extracting additional data from DW objects
- `SerializedPage` record: Add a `Permissions` list property

### Established Patterns
- YAML serialization via YamlDotNet with DoubleQuoted string style
- Records with init-only properties for DTOs
- Dictionary-based field storage for flexible data

### Integration Points
- `PermissionService` accessed via `new PermissionService()` or DI — check how DW10 instantiates it
- `AccessUser` lookup for group name resolution — may need `Services.Users` or direct repository access

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 11-permission-serialization*
*Context gathered: 2026-03-22*
