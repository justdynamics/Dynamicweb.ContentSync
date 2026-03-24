using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Commands;

/// <summary>
/// API-callable command that triggers deserialization for ALL configured providers.
/// Use via DW CLI: dw command SerializerDeserialize
/// Or via Management API: POST /Admin/Api/SerializerDeserialize
///
/// Uses SerializerOrchestrator to dispatch predicates to correct providers (Content, SqlTable, etc.).
/// </summary>
public sealed class SerializerDeserializeCommand : CommandBase
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

            var filesRoot = Path.GetDirectoryName(configPath)!;
            var systemDir = Path.Combine(filesRoot, "System");
            var paths = config.EnsureDirectories(systemDir);

            _logFile = LogFileWriter.CreateLogFile(paths.Log, "Deserialize");
            Log("=== Serializer Deserialize (API) started ===");

            if (!Directory.Exists(paths.SerializeRoot))
                return new() { Status = CommandResult.ResultType.Error, Message = $"SerializeRoot not found: {paths.SerializeRoot}" };

            var yamlCount = Directory.GetFiles(paths.SerializeRoot, "*.yml", SearchOption.AllDirectories).Length;
            if (yamlCount == 0)
                return new() { Status = CommandResult.ResultType.Error, Message = "SerializeRoot contains no YAML files" };

            var orchestrator = ProviderRegistry.CreateOrchestrator(filesRoot);
            var result = orchestrator.DeserializeAll(config.Predicates, paths.SerializeRoot, Log, config.DryRun);

            // Build summary with advice and flush log
            var advice = AdviceGenerator.GenerateAdvice(result);
            var summary = new LogFileSummary
            {
                Operation = "Deserialize",
                Timestamp = DateTime.UtcNow,
                DryRun = config.DryRun,
                Predicates = result.DeserializeResults.Select(r => new PredicateSummary
                {
                    Name = r.TableName,
                    Table = r.TableName,
                    Created = r.Created,
                    Updated = r.Updated,
                    Skipped = r.Skipped,
                    Failed = r.Failed,
                    Errors = r.Errors.ToList()
                }).ToList(),
                TotalCreated = result.DeserializeResults.Sum(r => r.Created),
                TotalUpdated = result.DeserializeResults.Sum(r => r.Updated),
                TotalSkipped = result.DeserializeResults.Sum(r => r.Skipped),
                TotalFailed = result.DeserializeResults.Sum(r => r.Failed),
                Errors = result.Errors.ToList(),
                Advice = advice
            };
            FlushLog(_logFile, summary);

            var message = result.Summary;
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
            return new() { Status = CommandResult.ResultType.Error, Message = $"Deserialization failed: {ex.Message}" };
        }
    }
}
