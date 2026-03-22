using Dynamicweb.ContentSync.AdminUI.Models;
using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.CoreUI.Data;

namespace Dynamicweb.ContentSync.AdminUI.Queries;

public sealed class PredicateByIndexQuery : DataQueryModelBase<PredicateEditModel>
{
    public int Index { get; set; } = -1;

    public override PredicateEditModel? GetModel()
    {
        if (Index < 0)
            return new PredicateEditModel();

        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null) return null;

        var config = ConfigLoader.Load(configPath);
        if (Index >= config.Predicates.Count) return null;

        var pred = config.Predicates[Index];
        return new PredicateEditModel
        {
            Index = Index,
            Name = pred.Name,
            AreaId = pred.AreaId,
            PageId = pred.PageId,
            Excludes = string.Join("\n", pred.Excludes)
        };
    }
}
