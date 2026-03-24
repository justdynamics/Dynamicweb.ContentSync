using DynamicWeb.Serializer.AdminUI.Models;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Queries;

public sealed class LogViewerQuery : DataQueryModelBase<LogViewerModel>
{
    public string? SelectedFileName { get; set; }

    public override LogViewerModel? GetModel()
    {
        return LogViewerModel.Load(SelectedFileName);
    }
}
