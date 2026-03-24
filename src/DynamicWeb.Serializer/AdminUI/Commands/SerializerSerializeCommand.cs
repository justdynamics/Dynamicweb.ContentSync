using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Commands;

/// <summary>
/// API-callable command that triggers serialization for ALL configured providers.
/// Use via DW CLI: dw command SerializerSerialize
/// Or via Management API: POST /Admin/Api/SerializerSerialize
///
/// Uses SerializerOrchestrator to dispatch predicates to correct providers (Content, SqlTable, etc.).
/// </summary>
public sealed class SerializerSerializeCommand : CommandBase
{
    private string? _logFile;
    private readonly List<string> _logLines = new();

    private void Log(string message)
    {
        _logLines.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }

    private void FlushLog(string logFile, LogFileSummary summary)
    {
        LogFileWriter.WriteSummaryHeader(logFile, summary);
        foreach (var line in _logLines)
            File.AppendAllText(logFile, line + "\n");
    }

    public override CommandResult Handle()
    {
        try
        {
            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath == null)
                return new() { Status = CommandResult.ResultType.Error, Message = "Serializer.config.json not found (also checked ContentSync.config.json)" };

            var config = ConfigLoader.Load(configPath);

            if (config.Predicates.Count == 0)
                return new() { Status = CommandResult.ResultType.Error, Message = "No predicates configured" };

            var filesRoot = Path.GetDirectoryName(configPath)!;
            var systemDir = Path.Combine(filesRoot, "System");
            var paths = config.EnsureDirectories(systemDir);

            _logFile = LogFileWriter.CreateLogFile(paths.Log, "Serialize");
            Log("=== Serializer Serialize (API) started ===");

            var orchestrator = ProviderRegistry.CreateOrchestrator(filesRoot);
            var result = orchestrator.SerializeAll(config.Predicates, paths.SerializeRoot, Log);

            var fileCount = Directory.Exists(paths.SerializeRoot)
                ? Directory.GetFiles(paths.SerializeRoot, "*.yml", SearchOption.AllDirectories).Length
                : 0;

            // Build summary and flush log
            var summary = new LogFileSummary
            {
                Operation = "Serialize",
                Timestamp = DateTime.UtcNow,
                DryRun = false,
                Predicates = result.SerializeResults.Select(r => new PredicateSummary
                {
                    Name = r.TableName,
                    Table = r.TableName,
                    Created = r.RowsSerialized
                }).ToList(),
                TotalCreated = result.SerializeResults.Sum(r => r.RowsSerialized),
                Errors = result.Errors.ToList()
            };
            FlushLog(_logFile, summary);

            var message = $"Serialization complete. {fileCount} YAML files written to {config.SerializeRoot}. {result.Summary}";
            if (result.HasErrors)
                message += $" Errors: {string.Join("; ", result.Errors)}";

            return new CommandResult
            {
                Status = result.HasErrors ? CommandResult.ResultType.Error : CommandResult.ResultType.Ok,
                Message = message
            };
        }
        catch (Exception ex)
        {
            return new() { Status = CommandResult.ResultType.Error, Message = $"Serialization failed: {ex.Message}" };
        }
    }
}
