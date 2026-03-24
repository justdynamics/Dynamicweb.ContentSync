using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.ContentSync.Models;
using Dynamicweb.ContentSync.Providers.SqlTable;
using Dynamicweb.CoreUI.Data;

namespace Dynamicweb.ContentSync.AdminUI.Commands;

/// <summary>
/// API-callable command that deserializes SQL tables via SqlTableProvider.
/// POST /Admin/Api/SqlTableDeserialize
/// </summary>
public sealed class SqlTableDeserializeCommand : CommandBase
{
    private string? _logFile;

    private void Log(string message)
    {
        if (_logFile == null) return;
        try { File.AppendAllText(_logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n"); } catch { }
    }

    public override CommandResult Handle()
    {
        try
        {
            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath == null)
                return new() { Status = CommandResult.ResultType.Error, Message = "ContentSync.config.json not found" };

            var config = ConfigLoader.Load(configPath);

            var filesRoot = Path.GetDirectoryName(configPath)!;
            var systemDir = Path.Combine(filesRoot, "System");
            var paths = config.EnsureDirectories(systemDir);

            _logFile = Path.Combine(paths.Log, "ContentSync.log");
            Log("=== SqlTable Deserialize (API) started ===");

            var rawJson = File.ReadAllText(configPath);
            var jsonDoc = System.Text.Json.JsonDocument.Parse(rawJson);
            var predicates = jsonDoc.RootElement.GetProperty("predicates");

            var sqlExecutor = new DwSqlExecutor();
            var metadataReader = new DataGroupMetadataReader(sqlExecutor);
            var tableReader = new SqlTableReader(sqlExecutor);
            var fileStore = new FlatFileStore();
            var writer = new SqlTableWriter(sqlExecutor);
            var provider = new SqlTableProvider(metadataReader, tableReader, fileStore, writer);

            var isDryRun = config.DryRun;
            var results = new List<string>();

            foreach (var pred in predicates.EnumerateArray())
            {
                var providerType = pred.TryGetProperty("providerType", out var pt) ? pt.GetString() : null;
                if (!string.Equals(providerType, "SqlTable", StringComparison.OrdinalIgnoreCase))
                    continue;

                var name = pred.GetProperty("name").GetString() ?? "unnamed";
                var table = pred.TryGetProperty("table", out var t) ? t.GetString() : null;
                var nameCol = pred.TryGetProperty("nameColumn", out var nc) ? nc.GetString() : null;
                var compareCols = pred.TryGetProperty("compareColumns", out var cc) ? cc.GetString() : null;

                if (string.IsNullOrEmpty(table))
                {
                    results.Add($"{name}: ERROR — missing 'table' field");
                    continue;
                }

                var predDef = new ProviderPredicateDefinition
                {
                    Name = name,
                    ProviderType = "SqlTable",
                    Table = table,
                    NameColumn = nameCol,
                    CompareColumns = compareCols
                };

                Log($"Deserializing: {name} (table: {table}, dryRun: {isDryRun})");
                var result = provider.Deserialize(predDef, paths.SerializeRoot, Log, isDryRun);
                results.Add($"{name}: {result.Created} created, {result.Updated} updated, {result.Skipped} skipped, {result.Failed} failed");
            }

            if (results.Count == 0)
                return new() { Status = CommandResult.ResultType.Error, Message = "No SqlTable predicates found in config" };

            return new CommandResult
            {
                Status = CommandResult.ResultType.Ok,
                Message = string.Join("\n", results)
            };
        }
        catch (Exception ex)
        {
            Log($"ERROR: {ex}");
            return new() { Status = CommandResult.ResultType.Error, Message = $"SqlTable deserialize failed: {ex.Message}" };
        }
    }
}
