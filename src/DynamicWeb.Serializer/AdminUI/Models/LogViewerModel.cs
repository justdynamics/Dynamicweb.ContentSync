using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Models;

public sealed class LogViewerModel : DataViewModelBase
{
    [ConfigurableProperty("Log File", explanation: "Select a log file to view")]
    public string SelectedFileName { get; set; } = string.Empty;

    [ConfigurableProperty("Has Logs")]
    public bool HasLogs { get; set; }

    public List<string> AvailableLogFiles { get; set; } = new();

    public LogFileSummary? Summary { get; set; }

    // Flattened summary fields for EditorFor binding
    [ConfigurableProperty("Operation", explanation: "The operation that produced this log")]
    public string SummaryOperation { get; set; } = string.Empty;

    [ConfigurableProperty("Timestamp", explanation: "When the operation ran")]
    public string SummaryTimestamp { get; set; } = string.Empty;

    [ConfigurableProperty("Dry Run", explanation: "Whether this was a dry-run operation")]
    public string SummaryDryRun { get; set; } = string.Empty;

    [ConfigurableProperty("Total Created", explanation: "Total items created")]
    public string SummaryTotalCreated { get; set; } = "0";

    [ConfigurableProperty("Total Updated", explanation: "Total items updated")]
    public string SummaryTotalUpdated { get; set; } = "0";

    [ConfigurableProperty("Total Skipped", explanation: "Total items skipped")]
    public string SummaryTotalSkipped { get; set; } = "0";

    [ConfigurableProperty("Total Failed", explanation: "Total items that failed")]
    public string SummaryTotalFailed { get; set; } = "0";

    [ConfigurableProperty("Predicate Breakdown", explanation: "Per-predicate summary")]
    public string PredicateBreakdown { get; set; } = string.Empty;

    [ConfigurableProperty("Advice", explanation: "Guided recommendations based on errors")]
    public string AdviceText { get; set; } = string.Empty;

    [ConfigurableProperty("Log Output", explanation: "Raw log text from the selected file")]
    public string RawLogText { get; set; } = string.Empty;

    /// <summary>
    /// Loads log viewer data from the configured log directory.
    /// </summary>
    public static LogViewerModel Load(string? selectedFile)
    {
        var model = new LogViewerModel();

        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null)
            return model;

        var config = ConfigLoader.Load(configPath);
        var filesRoot = Path.GetDirectoryName(configPath)!;
        var systemDir = Path.Combine(filesRoot, "System");
        var paths = config.EnsureDirectories(systemDir);

        var logFiles = LogFileWriter.GetLogFiles(paths.Log);
        if (logFiles.Length == 0)
            return model;

        model.HasLogs = true;
        model.AvailableLogFiles = logFiles.Select(f => f.Name).ToList();

        // Select the requested file, or default to most recent
        var targetFile = logFiles[0]; // most recent by default
        if (!string.IsNullOrEmpty(selectedFile))
        {
            var match = logFiles.FirstOrDefault(f =>
                f.Name.Equals(selectedFile, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                targetFile = match;
        }

        model.SelectedFileName = targetFile.Name;

        // Parse summary header
        var summary = LogFileWriter.ParseSummaryHeader(targetFile.FullName);
        model.Summary = summary;

        if (summary != null)
        {
            model.SummaryOperation = summary.Operation;
            model.SummaryTimestamp = summary.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            model.SummaryDryRun = summary.DryRun ? "Yes" : "No";
            model.SummaryTotalCreated = summary.TotalCreated.ToString();
            model.SummaryTotalUpdated = summary.TotalUpdated.ToString();
            model.SummaryTotalSkipped = summary.TotalSkipped.ToString();
            model.SummaryTotalFailed = summary.TotalFailed.ToString();

            // Build predicate breakdown text
            if (summary.Predicates.Count > 0)
            {
                model.PredicateBreakdown = string.Join(Environment.NewLine,
                    summary.Predicates.Select(p =>
                        $"{p.Name} ({p.Table}): Created={p.Created}, Updated={p.Updated}, Skipped={p.Skipped}, Failed={p.Failed}"));
            }

            // Build advice text
            if (summary.Advice.Count > 0)
            {
                model.AdviceText = string.Join(Environment.NewLine, summary.Advice);
            }
        }

        // Read raw log text (everything after the summary end marker)
        var content = File.ReadAllText(targetFile.FullName);
        const string endMarker = "=== END SUMMARY ===";
        var endIdx = content.IndexOf(endMarker, StringComparison.Ordinal);
        if (endIdx >= 0)
        {
            model.RawLogText = content[(endIdx + endMarker.Length)..].TrimStart('\r', '\n');
        }
        else
        {
            model.RawLogText = content;
        }

        return model;
    }
}
