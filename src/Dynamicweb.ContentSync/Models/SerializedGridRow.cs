namespace Dynamicweb.ContentSync.Models;

public record SerializedGridRow
{
    public required Guid Id { get; init; }
    public required int SortOrder { get; init; }
    public string? DefinitionId { get; init; }
    public string? ItemType { get; init; }
    public Dictionary<string, object> Fields { get; init; } = new();
    public List<SerializedGridColumn> Columns { get; init; } = new();
}
