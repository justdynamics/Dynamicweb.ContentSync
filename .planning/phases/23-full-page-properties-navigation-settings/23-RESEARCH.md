# Phase 23: Full Page Properties + Navigation Settings - Research

**Researched:** 2026-04-02
**Domain:** DynamicWeb 10 Page property serialization completeness + ecommerce navigation config
**Confidence:** HIGH

## Summary

Phase 23 extends the existing ContentMapper/ContentDeserializer pipeline to serialize and deserialize ~30 missing page properties plus the PageNavigationSettings ecommerce navigation object. All properties have public getters and setters on `Dynamicweb.Content.Page` and are persisted by `SavePage()`. NavigationSettings is NOT a separate table -- it stores inline on the Page table and is written by SavePage when `page.NavigationSettings != null`. Two string fields contain internal page links (`Default.aspx?ID=NNN`): `Page.ShortCut` and `PageNavigationSettings.ProductPage`. These require post-save link resolution using the existing `InternalLinkResolver`.

The DTO extension uses sub-record types for logical groupings (SEO, URL settings, visibility, navigation settings) to keep YAML readable. Boolean properties with non-false DW defaults (Allowclick=true, Allowsearch=true, ShowInSitemap=true, ShowInLegend=true) must have matching init defaults on the DTO to avoid breaking backward compatibility with older YAML files.

Critical discovery: `ActiveFrom` and `ActiveTo` are NOT nullable DateTime on the Page class -- they default to `DateTime.Now` and `DateHelper.MaxDate()` respectively. The DTO should use nullable DateTime to distinguish "not set in YAML" from actual values. `ShowInMenu` and `Published` are computed read-only properties (not settable) and must NOT be added to the DTO. `ShowUpdateDate` is marked `[Obsolete]` and should be skipped.

**Primary recommendation:** Extend SerializedPage with ~15 flat properties + 4 sub-objects, extend ContentMapper.MapPage() and ContentDeserializer.DeserializePage() (both INSERT and UPDATE paths), then extend ResolveLinksInArea() to also resolve ShortCut and ProductPage string values on each page.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PAGE-01 | All ~30 missing page properties serialized/deserialized | Complete property-to-DTO mapping verified against DW10 source. All have public setters, flow through SavePage. Sub-objects for SEO/URL/Visibility groupings. |
| PAGE-02 | ShortCut field `Default.aspx?ID=NNN` resolved via InternalLinkResolver | ShortCut is a plain string property. Existing InternalLinkResolver.ResolveLinks() handles the pattern. Needs extension in ResolveLinksInArea() to read page.ShortCut directly. |
| ECOM-01 | PageNavigationSettings serialized/deserialized (8 properties) | NavigationSettings is inline on Page table. All 8 properties verified. SavePage handles persistence when NavigationSettings != null. ParentType is enum (Groups=1, Shop=2). MaxLevels 100 = AllLevels. |
| ECOM-02 | ProductPage field in NavigationSettings resolved via InternalLinkResolver | ProductPage stores `Default.aspx?Id=NNN`. Same pattern as ShortCut. Needs same ResolveLinksInArea() extension. |
</phase_requirements>

## Standard Stack

### Core (unchanged)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Dynamicweb | 10.23.9 | DW10 Content API | Already referenced; Page, PageNavigationSettings, Services.Pages |
| YamlDotNet | 13.7.1 | YAML serialization | Already in use; handles new properties automatically |
| .NET | 8.0 | Runtime | Already in use |

No new dependencies needed. All APIs exist in the current NuGet package.

## Architecture Patterns

### Property Categorization (from DW10 Source Verification)

**Properties to ADD (verified with public setters on Page class):**

| Category | Properties | DTO Location |
|----------|-----------|-------------|
| Flat scalars | NavigationTag, ShortCut, Hidden, Allowclick, Allowsearch, ShowInSitemap, ShowInLegend, SslMode, ColorSchemeId, ExactUrl, ContentType, TopImage, DisplayMode, ActiveFrom, ActiveTo | SerializedPage (flat) |
| SEO group | MetaTitle, MetaCanonical, Description, Keywords, Noindex, Nofollow, Robots404 | SerializedSeoSettings sub-object |
| URL group | UrlDataProviderTypeName, UrlDataProviderParameters, UrlIgnoreForChildren, UrlUseAsWritten | SerializedUrlSettings sub-object |
| Visibility group | HideForPhones, HideForTablets, HideForDesktops | SerializedVisibilitySettings sub-object |
| Ecommerce nav | UseEcomGroups, ParentType, Groups, ShopID, MaxLevels, ProductPage, NavigationProvider, IncludeProducts | SerializedNavigationSettings sub-object |

**Properties explicitly NOT added (verified read-only or computed):**

| Property | Reason |
|----------|--------|
| ShowInMenu | COMPUTED: `Active && !Hidden` (no setter) |
| Published | COMPUTED: `!Hidden && isWithinActivePeriod` (no setter) |
| ShowUpdateDate | Marked `[Obsolete("Deprecated and will be removed")]` |
| Level | Computed from tree depth |
| IsMaster, IsLanguage | Computed from Area properties |
| MasterPageId, MasterType | Language/master system -- listed in REQUIREMENTS.md as out of scope for serialization |

### Boolean Default Values (CRITICAL for backward compatibility)

From DW10 Page.cs field initializers:

| Property | DW Default | DTO init value needed |
|----------|------------|----------------------|
| `_active` | `true` | Already handled (IsActive) |
| `_allowsearch` | `true` | `= true` |
| `_showInSitemap` | `true` | `= true` |
| `_allowclick` | `true` | `= true` |
| `_showInLegend` | `true` | `= true` |
| `_hidden` | `false` | `= false` (default) |
| `_noindex` | `false` | `= false` (default) |
| `_nofollow` | `false` | `= false` (default) |
| `_robots404` | `false` | `= false` (default) |
| All visibility bools | `false` | `= false` (default) |

### DateTime Handling for ActiveFrom/ActiveTo

DW10 Page.cs sets:
- `_activeFrom = DateTime.Now` (non-nullable)
- `_activeTo = DateHelper.MaxDate()` (non-nullable)

The DTO should use `DateTime?` to distinguish "YAML has no value" from "explicitly set". During deserialization, only set `page.ActiveFrom`/`page.ActiveTo` when the DTO value is non-null. This avoids overwriting DW's defaults with arbitrary values.

### DisplayMode Enum Serialization

```csharp
// DW10 DisplayMode enum:
public enum DisplayMode
{
    Normal = 0,  // Default
    List = 1     // Subpages shown in list view
}
```

Serialize as string name for YAML readability. Parse with `Enum.TryParse` on deserialization. Matches existing pattern (VerticalAlignment in GridRow).

### EcommerceNavigationParentType Enum

```csharp
public enum EcommerceNavigationParentType
{
    Groups = 1,
    Shop = 2
}
```

Serialize as string name. Parse with `Enum.TryParse` on deserialization.

### Recommended DTO Extension

```csharp
// New sub-records (new files in Models/)
public record SerializedSeoSettings
{
    public string? MetaTitle { get; init; }
    public string? MetaCanonical { get; init; }
    public string? Description { get; init; }
    public string? Keywords { get; init; }
    public bool Noindex { get; init; }
    public bool Nofollow { get; init; }
    public bool Robots404 { get; init; }
}

public record SerializedUrlSettings
{
    public string? UrlDataProviderTypeName { get; init; }
    public string? UrlDataProviderParameters { get; init; }
    public bool UrlIgnoreForChildren { get; init; }
    public bool UrlUseAsWritten { get; init; }
}

public record SerializedVisibilitySettings
{
    public bool HideForPhones { get; init; }
    public bool HideForTablets { get; init; }
    public bool HideForDesktops { get; init; }
}

public record SerializedNavigationSettings
{
    public bool UseEcomGroups { get; init; }
    public string? ParentType { get; init; }   // "Groups" or "Shop"
    public string? Groups { get; init; }        // Comma-separated group IDs or "[all]"
    public string? ShopID { get; init; }
    public int MaxLevels { get; init; }         // 100 = AllLevels
    public string? ProductPage { get; init; }   // "Default.aspx?Id=NNN" -- needs link resolution
    public string? NavigationProvider { get; init; }
    public bool IncludeProducts { get; init; }
}
```

Extended SerializedPage flat properties:
```csharp
// Add to existing SerializedPage record
public string? NavigationTag { get; init; }
public string? ShortCut { get; init; }         // "Default.aspx?ID=NNN" -- needs link resolution
public bool Hidden { get; init; }
public bool Allowclick { get; init; } = true;
public bool Allowsearch { get; init; } = true;
public bool ShowInSitemap { get; init; } = true;
public bool ShowInLegend { get; init; } = true;
public int SslMode { get; init; }
public string? ColorSchemeId { get; init; }
public string? ExactUrl { get; init; }
public string? ContentType { get; init; }
public string? TopImage { get; init; }
public string? DisplayMode { get; init; }      // "Normal" or "List"
public DateTime? ActiveFrom { get; init; }
public DateTime? ActiveTo { get; init; }

// Sub-objects
public SerializedSeoSettings? Seo { get; init; }
public SerializedUrlSettings? UrlSettings { get; init; }
public SerializedVisibilitySettings? Visibility { get; init; }
public SerializedNavigationSettings? NavigationSettings { get; init; }
```

### ContentMapper.MapPage() Extension Pattern

```csharp
// Add to existing MapPage method return statement:
NavigationTag = page.NavigationTag,
ShortCut = page.ShortCut,
Hidden = page.Hidden,
Allowclick = page.Allowclick,
Allowsearch = page.Allowsearch,
ShowInSitemap = page.ShowInSitemap,
ShowInLegend = page.ShowInLegend,
SslMode = page.SslMode,
ColorSchemeId = page.ColorSchemeId,
ExactUrl = page.ExactUrl,
ContentType = page.ContentType,
TopImage = page.TopImage,
DisplayMode = page.DisplayMode.ToString(),
ActiveFrom = page.ActiveFrom,
ActiveTo = page.ActiveTo,
Seo = new SerializedSeoSettings
{
    MetaTitle = page.MetaTitle,
    MetaCanonical = page.MetaCanonical,
    Description = page.Description,
    Keywords = page.Keywords,
    Noindex = page.Noindex,
    Nofollow = page.Nofollow,
    Robots404 = page.Robots404
},
UrlSettings = new SerializedUrlSettings
{
    UrlDataProviderTypeName = page.UrlDataProviderTypeName,
    UrlDataProviderParameters = page.UrlDataProviderParameters,
    UrlIgnoreForChildren = page.UrlIgnoreForChildren,
    UrlUseAsWritten = page.UrlUseAsWritten
},
Visibility = new SerializedVisibilitySettings
{
    HideForPhones = page.HideForPhones,
    HideForTablets = page.HideForTablets,
    HideForDesktops = page.HideForDesktops
},
NavigationSettings = MapNavigationSettings(page.NavigationSettings)
```

### ContentDeserializer Property Assignment Pattern

Both INSERT and UPDATE paths need the same property assignments:

```csharp
// Apply new scalar properties
page.NavigationTag = dto.NavigationTag ?? string.Empty;
page.ShortCut = dto.ShortCut ?? string.Empty;
page.Hidden = dto.Hidden;
page.Allowclick = dto.Allowclick;
page.Allowsearch = dto.Allowsearch;
page.ShowInSitemap = dto.ShowInSitemap;
page.ShowInLegend = dto.ShowInLegend;
page.SslMode = dto.SslMode;
page.ColorSchemeId = dto.ColorSchemeId;
page.ExactUrl = dto.ExactUrl;
page.ContentType = dto.ContentType;
page.TopImage = dto.TopImage;
if (Enum.TryParse<Dynamicweb.Content.DisplayMode>(dto.DisplayMode, true, out var dm))
    page.DisplayMode = dm;
if (dto.ActiveFrom.HasValue)
    page.ActiveFrom = dto.ActiveFrom.Value;
if (dto.ActiveTo.HasValue)
    page.ActiveTo = dto.ActiveTo.Value;

// Apply SEO sub-object
if (dto.Seo != null)
{
    page.MetaTitle = dto.Seo.MetaTitle;
    page.MetaCanonical = dto.Seo.MetaCanonical;
    page.Description = dto.Seo.Description;
    page.Keywords = dto.Seo.Keywords;
    page.Noindex = dto.Seo.Noindex;
    page.Nofollow = dto.Seo.Nofollow;
    page.Robots404 = dto.Seo.Robots404;
}

// Apply URL settings sub-object
if (dto.UrlSettings != null)
{
    page.UrlDataProviderTypeName = dto.UrlSettings.UrlDataProviderTypeName;
    page.UrlDataProviderParameters = dto.UrlSettings.UrlDataProviderParameters;
    page.UrlIgnoreForChildren = dto.UrlSettings.UrlIgnoreForChildren;
    page.UrlUseAsWritten = dto.UrlSettings.UrlUseAsWritten;
}

// Apply visibility sub-object
if (dto.Visibility != null)
{
    page.HideForPhones = dto.Visibility.HideForPhones;
    page.HideForTablets = dto.Visibility.HideForTablets;
    page.HideForDesktops = dto.Visibility.HideForDesktops;
}

// Apply NavigationSettings
if (dto.NavigationSettings != null && dto.NavigationSettings.UseEcomGroups)
{
    page.NavigationSettings = new PageNavigationSettings
    {
        UseEcomGroups = dto.NavigationSettings.UseEcomGroups,
        Groups = dto.NavigationSettings.Groups,
        ShopID = dto.NavigationSettings.ShopID,
        MaxLevels = dto.NavigationSettings.MaxLevels,
        ProductPage = dto.NavigationSettings.ProductPage,
        NavigationProvider = dto.NavigationSettings.NavigationProvider,
        IncludeProducts = dto.NavigationSettings.IncludeProducts
    };
    if (Enum.TryParse<EcommerceNavigationParentType>(
        dto.NavigationSettings.ParentType, true, out var pt))
        page.NavigationSettings.ParentType = pt;
}
```

### ShortCut + ProductPage Link Resolution Extension

The existing `ResolveLinksInArea()` iterates all pages and resolves ItemFields. Extend it to also resolve:

1. `page.ShortCut` -- read directly from the Page object, resolve, write back + SavePage
2. `page.NavigationSettings?.ProductPage` -- read from NavigationSettings, resolve, write back + SavePage

```csharp
// In ResolveLinksInArea(), after existing item field resolution:
foreach (var page in allPages)
{
    // ... existing item field resolution ...

    // Resolve ShortCut link (PAGE-02)
    bool pageNeedsResave = false;
    if (!string.IsNullOrEmpty(page.ShortCut))
    {
        var resolved = resolver.ResolveLinks(page.ShortCut);
        if (resolved != page.ShortCut)
        {
            page.ShortCut = resolved;
            pageNeedsResave = true;
        }
    }

    // Resolve NavigationSettings.ProductPage link (ECOM-02)
    if (page.NavigationSettings?.ProductPage != null)
    {
        var resolved = resolver.ResolveLinks(page.NavigationSettings.ProductPage);
        if (resolved != page.NavigationSettings.ProductPage)
        {
            page.NavigationSettings.ProductPage = resolved;
            pageNeedsResave = true;
        }
    }

    if (pageNeedsResave)
        Services.Pages.SavePage(page);
}
```

### Anti-Patterns to Avoid

- **Setting ShowInMenu/Published on Page:** These are computed read-only properties -- no setter exists
- **Setting ActiveFrom/ActiveTo unconditionally:** Only set when DTO has non-null values to avoid overwriting DW defaults
- **Creating NavigationSettings with UseEcomGroups=false:** DW only populates the object when UseEcomGroups is true. Setting it false and saving writes unnecessary columns.
- **Mapping all sub-objects unconditionally:** Only serialize NavigationSettings when UseEcomGroups is true. For SEO/URL/Visibility, always serialize (all pages have these).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Internal link rewriting | Custom string replacement | Existing `InternalLinkResolver.ResolveLinks()` | Boundary-aware regex, handles multiple patterns per string |
| Enum serialization | Custom string mapping | `Enum.TryParse<T>()` with `ToString()` | Standard .NET pattern, handles case-insensitive parsing |
| YAML sub-object serialization | Custom YAML writers | YamlDotNet automatic record serialization | Sub-records serialize to nested YAML automatically |

## Common Pitfalls

### Pitfall 1: Boolean Init Defaults Break Old YAML
**What goes wrong:** Old YAML files (pre-phase-23) lack `allowclick`, `allowsearch`, `showInSitemap`, `showInLegend`. YamlDotNet deserializes missing bools as `false`, changing page behavior.
**Why it happens:** C# `bool` defaults to `false`. DW defaults for these properties are `true`.
**How to avoid:** Set `{ get; init; } = true` on the DTO for all properties where DW default is true.
**Warning signs:** Pages become non-clickable, disappear from sitemaps/search after deserialization with old YAML.

### Pitfall 2: ActiveFrom/ActiveTo Non-Nullable in DW
**What goes wrong:** `Page.ActiveFrom` defaults to `DateTime.Now` and `Page.ActiveTo` defaults to `DateHelper.MaxDate()`. If DTO stores these and blindly writes them back, every page gets the serialization timestamp as ActiveFrom.
**Why it happens:** DW Page uses non-nullable DateTime with meaningful defaults.
**How to avoid:** DTO uses `DateTime?`. Only set page.ActiveFrom/ActiveTo when DTO value is non-null. When serializing, consider omitting values that match DW defaults (Now / MaxDate).
**Warning signs:** All pages show "active from" as the deserialization date.

### Pitfall 3: NavigationSettings Null vs Absent
**What goes wrong:** `PageRowExtractor` only creates NavigationSettings when UseEcomGroups=true in DB. Setting `page.NavigationSettings = new PageNavigationSettings { UseEcomGroups = false }` writes unnecessary columns. Setting it to null does not clear existing columns.
**Why it happens:** DW's AddNavigationSettingsUpdateStatement skips when NavigationSettings is null.
**How to avoid:** Only create NavigationSettings on the page when `dto.NavigationSettings != null && dto.NavigationSettings.UseEcomGroups`. Do not touch NavigationSettings otherwise.

### Pitfall 4: ShortCut Contains Various Link Formats
**What goes wrong:** ShortCut may contain `Default.aspx?ID=NNN` (internal), full URLs (external), or empty string. Only internal links need resolution.
**Why it happens:** ShortCut is a free-form string field in DW.
**How to avoid:** The existing InternalLinkResolver.ResolveLinks() only matches `Default.aspx?ID=\d+` patterns. Non-matching strings pass through unchanged. This is correct behavior.

### Pitfall 5: MaxLevels = 0 vs 100 vs AllLevels
**What goes wrong:** DW stores `MaxLevels > 10` as "AllLevels" string in DB. `Fill()` converts "AllLevels" to int 100. MaxLevels setter enforces minimum of 1 (`if (value <= 0) value = 1`).
**Why it happens:** DB stores string, API uses int, with special encoding.
**How to avoid:** Serialize as int. DW handles the AllLevels encoding on save automatically. The setter prevents 0.

### Pitfall 6: Null String Normalization
**What goes wrong:** DW returns null for unset string properties. YAML may deserialize empty strings. Setting `page.NavigationTag = ""` vs `page.NavigationTag = null` produces different DB values.
**Why it happens:** YamlDotNet treats `""` and missing keys differently.
**How to avoid:** Normalize empty strings to null (or use `?? string.Empty` consistently). Match existing ContentDeserializer pattern which uses `?? string.Empty` for required strings and passes nullable strings as-is.

## Code Examples

### NavigationSettings Serialization (Mapper)
```csharp
// Source: DW10 PageNavigationSettings.cs + PageRowExtractor behavior
private SerializedNavigationSettings? MapNavigationSettings(PageNavigationSettings? navSettings)
{
    if (navSettings == null || !navSettings.UseEcomGroups)
        return null;

    return new SerializedNavigationSettings
    {
        UseEcomGroups = true,
        ParentType = navSettings.ParentType.ToString(),  // "Groups" or "Shop"
        Groups = navSettings.Groups,
        ShopID = navSettings.ShopID,
        MaxLevels = navSettings.MaxLevels,
        ProductPage = navSettings.ProductPage,
        NavigationProvider = navSettings.NavigationProvider,
        IncludeProducts = navSettings.IncludeProducts
    };
}
```

### Helper Method for Common Property Assignment
```csharp
// Source: existing ContentDeserializer pattern
private static void ApplyPageProperties(Page page, SerializedPage dto)
{
    // Flat scalars
    page.NavigationTag = dto.NavigationTag;
    page.ShortCut = dto.ShortCut;
    page.Hidden = dto.Hidden;
    page.Allowclick = dto.Allowclick;
    page.Allowsearch = dto.Allowsearch;
    page.ShowInSitemap = dto.ShowInSitemap;
    page.ShowInLegend = dto.ShowInLegend;
    page.SslMode = dto.SslMode;
    page.ColorSchemeId = dto.ColorSchemeId;
    page.ExactUrl = dto.ExactUrl;
    page.ContentType = dto.ContentType;
    page.TopImage = dto.TopImage;

    if (!string.IsNullOrEmpty(dto.DisplayMode) &&
        Enum.TryParse<Dynamicweb.Content.DisplayMode>(dto.DisplayMode, true, out var dm))
        page.DisplayMode = dm;

    if (dto.ActiveFrom.HasValue)
        page.ActiveFrom = dto.ActiveFrom.Value;
    if (dto.ActiveTo.HasValue)
        page.ActiveTo = dto.ActiveTo.Value;

    // SEO
    if (dto.Seo != null)
    {
        page.MetaTitle = dto.Seo.MetaTitle;
        page.MetaCanonical = dto.Seo.MetaCanonical;
        page.Description = dto.Seo.Description;
        page.Keywords = dto.Seo.Keywords;
        page.Noindex = dto.Seo.Noindex;
        page.Nofollow = dto.Seo.Nofollow;
        page.Robots404 = dto.Seo.Robots404;
    }

    // URL settings
    if (dto.UrlSettings != null)
    {
        page.UrlDataProviderTypeName = dto.UrlSettings.UrlDataProviderTypeName;
        page.UrlDataProviderParameters = dto.UrlSettings.UrlDataProviderParameters;
        page.UrlIgnoreForChildren = dto.UrlSettings.UrlIgnoreForChildren;
        page.UrlUseAsWritten = dto.UrlSettings.UrlUseAsWritten;
    }

    // Visibility
    if (dto.Visibility != null)
    {
        page.HideForPhones = dto.Visibility.HideForPhones;
        page.HideForTablets = dto.Visibility.HideForTablets;
        page.HideForDesktops = dto.Visibility.HideForDesktops;
    }

    // NavigationSettings
    if (dto.NavigationSettings != null && dto.NavigationSettings.UseEcomGroups)
    {
        page.NavigationSettings = new PageNavigationSettings
        {
            UseEcomGroups = true,
            Groups = dto.NavigationSettings.Groups,
            ShopID = dto.NavigationSettings.ShopID,
            MaxLevels = dto.NavigationSettings.MaxLevels,
            ProductPage = dto.NavigationSettings.ProductPage,
            NavigationProvider = dto.NavigationSettings.NavigationProvider,
            IncludeProducts = dto.NavigationSettings.IncludeProducts
        };
        if (Enum.TryParse<EcommerceNavigationParentType>(
            dto.NavigationSettings.ParentType, true, out var pt))
            page.NavigationSettings.ParentType = pt;
    }
}
```

## Files to Modify

| File | Change | Complexity |
|------|--------|-----------|
| `Models/SerializedPage.cs` | Add ~15 flat properties + 4 sub-object references | Low |
| `Models/SerializedSeoSettings.cs` | NEW: 7-property sub-record | Low |
| `Models/SerializedUrlSettings.cs` | NEW: 4-property sub-record | Low |
| `Models/SerializedVisibilitySettings.cs` | NEW: 3-property sub-record | Low |
| `Models/SerializedNavigationSettings.cs` | NEW: 8-property sub-record | Low |
| `Serialization/ContentMapper.cs` | Extend MapPage() + add MapNavigationSettings() helper | Low-Med |
| `Serialization/ContentDeserializer.cs` | Extend INSERT + UPDATE paths + ResolveLinksInArea() | Medium |
| `Tests/Models/DtoTests.cs` | Add tests for new sub-records + boolean defaults | Low |
| `Tests/Infrastructure/YamlRoundTripTests.cs` | Add round-trip tests for sub-objects | Low |

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 + Moq 4.20.72 |
| Config file | `tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj` |
| Quick run command | `dotnet test tests/DynamicWeb.Serializer.Tests --no-build -v q` |
| Full suite command | `dotnet test --no-build -v q` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PAGE-01 | New DTO properties have correct defaults | unit | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "DtoTests" --no-build -v q` | Needs extension |
| PAGE-01 | New properties round-trip through YAML | unit | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "YamlRoundTripTests" --no-build -v q` | Needs extension |
| PAGE-01 | Old YAML (without new props) deserializes with correct boolean defaults | unit | New test | Wave 0 |
| PAGE-02 | ShortCut with Default.aspx?ID=NNN is resolved | unit | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "InternalLinkResolverTests" --no-build -v q` | Exists, needs no change (resolver already handles pattern) |
| ECOM-01 | NavigationSettings sub-object round-trips | unit | New test in YamlRoundTripTests | Wave 0 |
| ECOM-02 | ProductPage link resolution | unit | Same InternalLinkResolverTests | Exists |

### Sampling Rate
- **Per task commit:** `dotnet test tests/DynamicWeb.Serializer.Tests --no-build -v q`
- **Per wave merge:** `dotnet test --no-build -v q`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] Extend `DtoTests.cs` -- boolean defaults test (Allowclick=true etc)
- [ ] Extend `YamlRoundTripTests.cs` -- sub-objects round-trip + backward compatibility test
- [ ] Extend `YamlRoundTripTests.cs` -- NavigationSettings round-trip
- [ ] No new test infrastructure needed -- existing xunit/Moq setup covers all requirements

## Open Questions

1. **ActiveFrom/ActiveTo serialization strategy**
   - What we know: DW defaults to DateTime.Now / MaxDate. Non-nullable on Page.
   - What's unclear: Should we serialize these for every page (producing large YAML diffs on re-serialize) or only when they differ from defaults?
   - Recommendation: Always serialize them. They are meaningful dates. Accept that re-serializing will show the serialization timestamp for ActiveFrom on pages where it was DateTime.Now.

2. **NavigationSettings.Groups portability**
   - What we know: Groups contains comma-separated ecommerce group IDs. These are environment-specific.
   - What's unclear: Can these be resolved like page IDs?
   - Recommendation: Serialize as-is per REQUIREMENTS.md ("deferred to future"). Document as known limitation.

## Sources

### Primary (HIGH confidence)
- DW10 source: `C:\Projects\temp\dw10source\src\Features\Content\Dynamicweb\Content\Page.cs` -- all property definitions, defaults, getters/setters
- DW10 source: `C:\Projects\temp\dw10source\src\Features\Content\Dynamicweb\Content\PageNavigationSettings.cs` -- complete NavigationSettings class with Fill() method
- DW10 source: `C:\Projects\temp\dw10source\src\Features\Content\Dynamicweb\Content\Data\PageRepository.cs` -- InsertPage/UpdatePage SQL confirming all properties persisted
- DW10 source: `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Core\Entity.cs` -- Entity<T> base class, Audit handling, SetProperty/MarkAsUpdated

### Secondary (HIGH confidence)
- Existing codebase: ContentMapper.cs, ContentDeserializer.cs, InternalLinkResolver.cs, SerializedPage.cs -- current implementation patterns
- Prior research: `.planning/research/STACK.md`, `ARCHITECTURE.md`, `PITFALLS.md` -- verified property mappings

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - no new dependencies, all APIs verified in DW10 source
- Architecture: HIGH - extending existing patterns (MapPage, DeserializePage, ResolveLinksInArea)
- Pitfalls: HIGH - boolean defaults and DateTime handling verified against DW10 source field initializers
- Link resolution: HIGH - existing InternalLinkResolver handles the exact pattern needed

**Research date:** 2026-04-02
**Valid until:** 2026-05-02 (stable -- DW 10.23.9 API, no breaking changes expected)
