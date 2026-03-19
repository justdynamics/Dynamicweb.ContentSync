namespace Dynamicweb.ContentSync.Models;

public record SerializedGridRow
{
    public required Guid Id { get; init; }
    public required int SortOrder { get; init; }
    public List<SerializedGridColumn> Columns { get; init; } = new();
}
