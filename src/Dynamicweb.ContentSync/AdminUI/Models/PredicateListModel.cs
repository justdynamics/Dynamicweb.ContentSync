using Dynamicweb.CoreUI.Data;

namespace Dynamicweb.ContentSync.AdminUI.Models;

public sealed class PredicateListModel : DataViewModelBase
{
    public int Index { get; set; }

    [ConfigurableProperty("Name")]
    public string Name { get; set; } = string.Empty;

    [ConfigurableProperty("Path")]
    public string Path { get; set; } = string.Empty;

    [ConfigurableProperty("Area")]
    public string AreaName { get; set; } = string.Empty;
}
