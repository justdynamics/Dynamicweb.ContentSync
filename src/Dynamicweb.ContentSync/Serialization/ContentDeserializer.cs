using Dynamicweb.Content;
using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.ContentSync.Infrastructure;
using Dynamicweb.ContentSync.Models;

namespace Dynamicweb.ContentSync.Serialization;

/// <summary>
/// Orchestrates the disk-to-DynamicWeb deserialization pipeline:
/// reads YAML files via FileSystemStore.ReadTree(), resolves GUID identity against
/// the target database, writes items in dependency order (Area > Pages > GridRows > Paragraphs),
/// supports dry-run mode with field-level diffs, and handles errors with cascade-skip semantics.
/// </summary>
public class ContentDeserializer
{
    private readonly SyncConfiguration _configuration;
    private readonly IContentStore _store;
    private readonly Action<string>? _log;
    private readonly bool _isDryRun;

    public ContentDeserializer(
        SyncConfiguration configuration,
        IContentStore? store = null,
        Action<string>? log = null,
        bool isDryRun = false)
    {
        _configuration = configuration;
        _store = store ?? new FileSystemStore();
        _log = log;
        _isDryRun = isDryRun;
    }

    private void Log(string message) => _log?.Invoke(message);

    // -------------------------------------------------------------------------
    // Write context — carries mutable state through the recursive tree walk
    // -------------------------------------------------------------------------

    private class WriteContext
    {
        public int TargetAreaId { get; set; }
        public int ParentPageId { get; set; }  // 0 for root pages
        public Dictionary<Guid, int> PageGuidCache { get; set; } = new();
        public HashSet<Guid> FailedParentGuids { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
    }

    // -------------------------------------------------------------------------
    // Public entry point
    // -------------------------------------------------------------------------

    /// <summary>
    /// Deserializes all predicates defined in the configuration from disk to DW.
    /// Returns a DeserializeResult with aggregate counts and any errors encountered.
    /// </summary>
    public DeserializeResult Deserialize()
    {
        var area = _store.ReadTree(_configuration.OutputDirectory);

        int totalCreated = 0;
        int totalUpdated = 0;
        int totalSkipped = 0;
        int totalFailed = 0;
        var allErrors = new List<string>();

        foreach (var predicate in _configuration.Predicates)
        {
            var result = DeserializePredicate(predicate, area);
            totalCreated += result.Created;
            totalUpdated += result.Updated;
            totalSkipped += result.Skipped;
            totalFailed += result.Failed;
            allErrors.AddRange(result.Errors);
        }

        var aggregated = new DeserializeResult
        {
            Created = totalCreated,
            Updated = totalUpdated,
            Skipped = totalSkipped,
            Failed = totalFailed,
            Errors = allErrors
        };

        Log(aggregated.Summary);
        if (aggregated.HasErrors)
        {
            foreach (var error in aggregated.Errors)
                Log(error);
        }

        return aggregated;
    }

    // -------------------------------------------------------------------------
    // Predicate-level processing
    // -------------------------------------------------------------------------

    private DeserializeResult DeserializePredicate(PredicateDefinition predicate, SerializedArea area)
    {
        var targetArea = Services.Areas.GetArea(predicate.AreaId);
        if (targetArea == null)
        {
            Log($"Warning: Area with ID {predicate.AreaId} not found. Skipping predicate '{predicate.Name}'.");
            return new DeserializeResult();
        }

        Log($"Deserializing predicate '{predicate.Name}' into area ID={predicate.AreaId}");

        // Pre-build page GUID cache for the entire area (avoids per-item full table scans)
        var allPages = Services.Pages.GetPagesByAreaID(predicate.AreaId);
        var pageGuidCache = allPages
            .Where(p => p.UniqueId != Guid.Empty)
            .ToDictionary(p => p.UniqueId, p => p.ID);

        var ctx = new WriteContext
        {
            TargetAreaId = predicate.AreaId,
            ParentPageId = 0,
            PageGuidCache = pageGuidCache
        };

        foreach (var page in area.Pages)
        {
            DeserializePageSafe(page, ctx);
        }

        return new DeserializeResult
        {
            Created = ctx.Created,
            Updated = ctx.Updated,
            Skipped = ctx.Skipped,
            Failed = ctx.Failed,
            Errors = ctx.Errors
        };
    }

    // -------------------------------------------------------------------------
    // Page deserialization
    // -------------------------------------------------------------------------

    private void DeserializePageSafe(SerializedPage dto, WriteContext ctx)
    {
        // Cascade skip: if any ancestor failed, skip this page and all its children
        if (ctx.FailedParentGuids.Contains(dto.PageUniqueId))
        {
            ctx.Skipped++;
            Log($"SKIPPED page {dto.PageUniqueId} ('{dto.MenuText}') — parent failed");
            return;
        }

        // Check if any ancestor of this page is in the failed set by traversal context
        // (FailedParentGuids accumulates failed pages; children have their parent GUID tracked separately)
        // The cascade skip check above handles direct parent matching; the broader check is handled
        // by not recursing into children when a parent throws (implicit via exception handling below)

        try
        {
            int resolvedId = DeserializePage(dto, ctx);

            // In dry-run mode, don't attempt grid rows/children with synthetic -1 ID
            if (resolvedId < 0 && _isDryRun)
            {
                // Still log children would be processed
                foreach (var child in dto.Children)
                {
                    Log($"[DRY-RUN] SKIP child {child.PageUniqueId} ('{child.MenuText}') — parent is CREATE in dry-run");
                    ctx.Skipped++;
                }
                return;
            }

            if (resolvedId < 0)
                return;

            // Process grid rows for this page
            var gridRowCache = Services.Grids.GetGridRowsByPageId(resolvedId)
                .Where(gr => gr.UniqueId != Guid.Empty)
                .ToDictionary(gr => gr.UniqueId, gr => gr.ID);

            foreach (var row in dto.GridRows)
            {
                DeserializeGridRowSafe(row, resolvedId, gridRowCache, ctx);
            }

            // Recurse children with this page as parent
            var savedParentPageId = ctx.ParentPageId;
            ctx.ParentPageId = resolvedId;
            foreach (var child in dto.Children)
            {
                DeserializePageSafe(child, ctx);
            }
            ctx.ParentPageId = savedParentPageId;
        }
        catch (Exception ex)
        {
            ctx.Failed++;
            var msg = $"ERROR deserializing page {dto.PageUniqueId} ('{dto.MenuText}'): {ex.Message}";
            ctx.Errors.Add(msg);
            Log(msg);

            // Mark this page as failed so all descendant pages are cascade-skipped
            ctx.FailedParentGuids.Add(dto.PageUniqueId);
            Log($"  SKIPPED children of {dto.PageUniqueId} due to parent failure");
        }
    }

    /// <summary>
    /// Writes a single page to DW (insert or update). Returns the resolved numeric page ID,
    /// or -1 in dry-run CREATE mode (no DW ID assigned).
    /// </summary>
    private int DeserializePage(SerializedPage dto, WriteContext ctx)
    {
        if (!ctx.PageGuidCache.TryGetValue(dto.PageUniqueId, out var existingId))
        {
            // INSERT path — GUID not found in target area
            if (_isDryRun)
            {
                Log($"[DRY-RUN] CREATE page {dto.PageUniqueId} ('{dto.MenuText}')");
                foreach (var f in dto.Fields)
                    Log($"  set {f.Key} = '{f.Value}'");
                ctx.Created++;
                return -1;
            }

            var page = new Page();
            page.UniqueId = dto.PageUniqueId;
            page.AreaId = ctx.TargetAreaId;
            page.ParentPageId = ctx.ParentPageId;
            page.MenuText = dto.MenuText;
            page.UrlName = dto.UrlName;
            page.Active = dto.IsActive;
            page.Sort = dto.SortOrder;
            // Do NOT set page.ID — leave 0 for insert path (Pitfall 4)

            var saved = Services.Pages.SavePage(page);
            ctx.PageGuidCache[dto.PageUniqueId] = saved.ID;

            // Apply ItemType fields in a post-save update (Pitfall 3: Item may be null before first save)
            if (dto.Fields.Count > 0)
            {
                var refetched = Services.Pages.GetPage(saved.ID);
                if (refetched?.Item != null)
                {
                    foreach (var kvp in dto.Fields)
                    {
                        refetched.Item[kvp.Key] = kvp.Value;
                    }
                    Services.Pages.SavePage(refetched);
                }
            }

            ctx.Created++;
            Log($"CREATED page {dto.PageUniqueId} -> ID={saved.ID}");
            return saved.ID;
        }
        else
        {
            // UPDATE path — GUID matched an existing page
            // Load existing page from DW so it has an internally-set ID (DW Entity<int>.ID has no public setter)
            var existingPage = Services.Pages.GetPage(existingId);
            if (existingPage == null)
            {
                throw new InvalidOperationException(
                    $"Could not load existing page with ID {existingId} for update.");
            }

            if (_isDryRun)
            {
                LogDryRunPageUpdate(dto, existingPage, ctx);
                return existingId;
            }

            // Apply scalar properties (source-wins)
            existingPage.UniqueId = dto.PageUniqueId;
            existingPage.AreaId = ctx.TargetAreaId;
            existingPage.ParentPageId = ctx.ParentPageId;
            existingPage.MenuText = dto.MenuText;
            existingPage.UrlName = dto.UrlName;
            existingPage.Active = dto.IsActive;
            existingPage.Sort = dto.SortOrder;

            // Apply ItemType fields with full replace (source-wins)
            if (existingPage.Item != null)
            {
                // Clear fields that exist in DB but not in YAML
                foreach (var fieldName in existingPage.Item.Names.ToList())
                {
                    if (!dto.Fields.ContainsKey(fieldName))
                        existingPage.Item[fieldName] = null;
                }
                // Apply all fields from YAML
                foreach (var kvp in dto.Fields)
                {
                    existingPage.Item[kvp.Key] = kvp.Value;
                }
            }

            Services.Pages.SavePage(existingPage);
            ctx.Updated++;
            Log($"UPDATED page {dto.PageUniqueId} (ID={existingId})");
            return existingId;
        }
    }

    // -------------------------------------------------------------------------
    // Grid row deserialization
    // -------------------------------------------------------------------------

    private void DeserializeGridRowSafe(
        SerializedGridRow dto,
        int pageId,
        Dictionary<Guid, int> gridRowCache,
        WriteContext ctx)
    {
        try
        {
            int resolvedGridRowId = DeserializeGridRow(dto, pageId, gridRowCache, ctx);

            if (resolvedGridRowId < 0 && _isDryRun)
                return;

            if (resolvedGridRowId < 0)
                return;

            // Build paragraph GUID cache for this page
            var paragraphCache = Services.Paragraphs.GetParagraphsByPageId(pageId)
                .Where(p => p.UniqueId != Guid.Empty)
                .ToDictionary(p => p.UniqueId, p => p.ID);

            foreach (var column in dto.Columns)
            {
                foreach (var para in column.Paragraphs)
                {
                    DeserializeParagraphSafe(para, pageId, resolvedGridRowId, column.Id, paragraphCache, ctx);
                }
            }
        }
        catch (Exception ex)
        {
            ctx.Failed++;
            var msg = $"ERROR deserializing grid row {dto.Id} on page {pageId}: {ex.Message}";
            ctx.Errors.Add(msg);
            Log(msg);
        }
    }

    private int DeserializeGridRow(
        SerializedGridRow dto,
        int pageId,
        Dictionary<Guid, int> gridRowCache,
        WriteContext ctx)
    {
        if (!gridRowCache.TryGetValue(dto.Id, out var existingGridRowId))
        {
            // INSERT path
            if (_isDryRun)
            {
                Log($"[DRY-RUN] CREATE grid row {dto.Id} (sort={dto.SortOrder}) on page {pageId}");
                ctx.Created++;
                return -1;
            }

            var row = new GridRow(pageId);
            row.UniqueId = dto.Id;
            row.Sort = dto.SortOrder;
            // Do NOT set row.ID (insert path)

            Services.Grids.SaveGridRow(row);

            // Re-query to get DW-assigned numeric ID (Pitfall 1: SaveGridRow returns bool, not GridRow)
            var saved = Services.Grids.GetGridRowsByPageId(pageId)
                .FirstOrDefault(gr => gr.UniqueId == dto.Id);

            if (saved == null)
                throw new InvalidOperationException($"Could not find inserted grid row with GUID {dto.Id}");

            var newGridRowId = saved.ID;
            ctx.Created++;
            Log($"CREATED grid row {dto.Id} -> ID={newGridRowId} on page {pageId}");
            return newGridRowId;
        }
        else
        {
            // UPDATE path
            if (_isDryRun)
            {
                // Fetch existing to compare sort order
                var existingRows = Services.Grids.GetGridRowsByPageId(pageId);
                var existingRow = existingRows.FirstOrDefault(gr => gr.ID == existingGridRowId);
                if (existingRow != null && existingRow.Sort != dto.SortOrder)
                {
                    Log($"[DRY-RUN] UPDATE grid row {dto.Id} (ID={existingGridRowId}): Sort: {existingRow.Sort} -> {dto.SortOrder}");
                    ctx.Updated++;
                }
                else
                {
                    Log($"[DRY-RUN] SKIP grid row {dto.Id} (ID={existingGridRowId}) (unchanged)");
                    ctx.Skipped++;
                }
                return existingGridRowId;
            }

            // Load existing grid row from DW so it has internally-set ID (DW Entity<int>.ID has no public setter)
            var existingRow2 = Services.Grids.GetGridRowsByPageId(pageId)
                .FirstOrDefault(gr => gr.ID == existingGridRowId);
            if (existingRow2 == null)
            {
                throw new InvalidOperationException(
                    $"Could not load existing grid row with ID {existingGridRowId} for update.");
            }

            existingRow2.UniqueId = dto.Id;
            existingRow2.Sort = dto.SortOrder;

            Services.Grids.SaveGridRow(existingRow2);
            ctx.Updated++;
            Log($"UPDATED grid row {dto.Id} (ID={existingGridRowId})");
            return existingGridRowId;
        }
    }

    // -------------------------------------------------------------------------
    // Paragraph deserialization
    // -------------------------------------------------------------------------

    private void DeserializeParagraphSafe(
        SerializedParagraph dto,
        int pageId,
        int gridRowId,
        int columnId,
        Dictionary<Guid, int> paragraphCache,
        WriteContext ctx)
    {
        try
        {
            DeserializeParagraph(dto, pageId, gridRowId, columnId, paragraphCache, ctx);
        }
        catch (Exception ex)
        {
            ctx.Failed++;
            var msg = $"ERROR deserializing paragraph {dto.ParagraphUniqueId} on page {pageId}: {ex.Message}";
            ctx.Errors.Add(msg);
            Log(msg);
        }
    }

    private void DeserializeParagraph(
        SerializedParagraph dto,
        int pageId,
        int gridRowId,
        int columnId,
        Dictionary<Guid, int> paragraphCache,
        WriteContext ctx)
    {
        if (!paragraphCache.TryGetValue(dto.ParagraphUniqueId, out var existingParagraphId))
        {
            // INSERT path
            if (_isDryRun)
            {
                Log($"[DRY-RUN] CREATE paragraph {dto.ParagraphUniqueId} (sort={dto.SortOrder}, type={dto.ItemType}) on page {pageId}");
                foreach (var f in dto.Fields)
                    Log($"  set {f.Key} = '{f.Value}'");
                ctx.Created++;
                return;
            }

            var para = new Paragraph();
            para.UniqueId = dto.ParagraphUniqueId;
            para.PageID = pageId;
            para.GridRowId = gridRowId;
            para.GridRowColumn = columnId;
            para.Sort = dto.SortOrder;
            para.Header = dto.Header;
            para.ItemType = dto.ItemType;
            // Do NOT set para.ID (insert path)

            Services.Paragraphs.SaveParagraph(para);

            // Re-query to get assigned ID and apply ItemType fields (Pitfall 3: Item may be null before save)
            var saved = Services.Paragraphs.GetParagraphsByPageId(pageId)
                .FirstOrDefault(p => p.UniqueId == dto.ParagraphUniqueId);

            if (saved != null && dto.Fields.Count > 0)
            {
                foreach (var kvp in dto.Fields)
                {
                    if (kvp.Key == "Text")
                        saved.Text = kvp.Value?.ToString();
                    else if (saved.Item != null)
                        saved.Item[kvp.Key] = kvp.Value;
                }
                Services.Paragraphs.SaveParagraph(saved);
            }

            ctx.Created++;
            Log($"CREATED paragraph {dto.ParagraphUniqueId} on page {pageId}");
        }
        else
        {
            // UPDATE path
            if (_isDryRun)
            {
                var existingParagraphs = Services.Paragraphs.GetParagraphsByPageId(pageId);
                var existing = existingParagraphs.FirstOrDefault(p => p.ID == existingParagraphId);
                if (existing != null)
                    LogDryRunParagraphUpdate(dto, existing, ctx);
                return;
            }

            // Load existing paragraph for update
            var existingForUpdate = Services.Paragraphs.GetParagraphsByPageId(pageId)
                .FirstOrDefault(p => p.ID == existingParagraphId);

            if (existingForUpdate == null)
            {
                throw new InvalidOperationException(
                    $"Could not load existing paragraph with ID {existingParagraphId} for update.");
            }

            existingForUpdate.UniqueId = dto.ParagraphUniqueId;
            existingForUpdate.GridRowId = gridRowId;
            existingForUpdate.GridRowColumn = columnId;
            existingForUpdate.Sort = dto.SortOrder;
            existingForUpdate.Header = dto.Header;
            existingForUpdate.ItemType = dto.ItemType;

            // Apply ItemType fields with full replace (source-wins)
            if (existingForUpdate.Item != null)
            {
                // Clear fields that exist in DB but not in YAML
                foreach (var fieldName in existingForUpdate.Item.Names.ToList())
                {
                    if (!dto.Fields.ContainsKey(fieldName))
                        existingForUpdate.Item[fieldName] = null;
                }
            }

            // Write all fields from YAML
            foreach (var kvp in dto.Fields)
            {
                if (kvp.Key == "Text")
                    existingForUpdate.Text = kvp.Value?.ToString();
                else if (existingForUpdate.Item != null)
                    existingForUpdate.Item[kvp.Key] = kvp.Value;
            }

            Services.Paragraphs.SaveParagraph(existingForUpdate);
            ctx.Updated++;
            Log($"UPDATED paragraph {dto.ParagraphUniqueId} (ID={existingParagraphId})");
        }
    }

    // -------------------------------------------------------------------------
    // Dry-run diff logging
    // -------------------------------------------------------------------------

    private void LogDryRunPageUpdate(SerializedPage dto, Page? existing, WriteContext ctx)
    {
        if (existing == null)
        {
            Log($"[DRY-RUN] UPDATE page {dto.PageUniqueId} (could not load existing for diff)");
            ctx.Updated++;
            return;
        }

        var diffs = new List<string>();

        if (dto.MenuText != existing.MenuText)
            diffs.Add($"MenuText: '{existing.MenuText}' -> '{dto.MenuText}'");
        if (dto.UrlName != existing.UrlName)
            diffs.Add($"UrlName: '{existing.UrlName}' -> '{dto.UrlName}'");
        if (dto.IsActive != existing.Active)
            diffs.Add($"Active: {existing.Active} -> {dto.IsActive}");
        if (dto.SortOrder != existing.Sort)
            diffs.Add($"Sort: {existing.Sort} -> {dto.SortOrder}");

        // Field-level diffs for ItemType fields
        foreach (var kvp in dto.Fields)
        {
            var currentVal = existing.Item?[kvp.Key]?.ToString();
            var newVal = kvp.Value?.ToString();
            if (currentVal != newVal)
                diffs.Add($"Fields[{kvp.Key}]: '{currentVal}' -> '{newVal}'");
        }

        if (diffs.Count == 0)
        {
            Log($"[DRY-RUN] SKIP {dto.PageUniqueId} (unchanged)");
            ctx.Skipped++;
        }
        else
        {
            Log($"[DRY-RUN] UPDATE {dto.PageUniqueId}:\n  " + string.Join("\n  ", diffs));
            ctx.Updated++;
        }
    }

    private void LogDryRunParagraphUpdate(SerializedParagraph dto, Paragraph existing, WriteContext ctx)
    {
        var diffs = new List<string>();

        if (dto.SortOrder != existing.Sort)
            diffs.Add($"Sort: {existing.Sort} -> {dto.SortOrder}");
        if (dto.Header != existing.Header)
            diffs.Add($"Header: '{existing.Header}' -> '{dto.Header}'");
        if (dto.ItemType != existing.ItemType)
            diffs.Add($"ItemType: '{existing.ItemType}' -> '{dto.ItemType}'");

        // Field-level diffs for ItemType fields
        foreach (var kvp in dto.Fields)
        {
            string? currentVal;
            if (kvp.Key == "Text")
                currentVal = existing.Text;
            else
                currentVal = existing.Item?[kvp.Key]?.ToString();

            var newVal = kvp.Value?.ToString();
            if (currentVal != newVal)
                diffs.Add($"Fields[{kvp.Key}]: '{currentVal}' -> '{newVal}'");
        }

        if (diffs.Count == 0)
        {
            Log($"[DRY-RUN] SKIP {dto.ParagraphUniqueId} (unchanged)");
            ctx.Skipped++;
        }
        else
        {
            Log($"[DRY-RUN] UPDATE {dto.ParagraphUniqueId}:\n  " + string.Join("\n  ", diffs));
            ctx.Updated++;
        }
    }
}
