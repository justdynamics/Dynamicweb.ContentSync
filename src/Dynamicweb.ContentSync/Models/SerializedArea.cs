namespace Dynamicweb.ContentSync.Models;

public record SerializedArea
{
    public required Guid AreaId { get; init; }
    public required string Name { get; init; }
    public required int SortOrder { get; init; }
    public List<SerializedPage> Pages { get; init; } = new();
}
