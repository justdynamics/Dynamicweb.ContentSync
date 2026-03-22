using Dynamicweb.ContentSync.AdminUI.Commands;
using Dynamicweb.ContentSync.AdminUI.Models;
using Dynamicweb.CoreUI.Actions;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Displays.Information;
using Dynamicweb.CoreUI.Editors;
using Dynamicweb.CoreUI.Editors.Inputs;
using Dynamicweb.CoreUI.Editors.Lists;
using Dynamicweb.CoreUI.Screens;
using static Dynamicweb.CoreUI.Editors.Inputs.ListBase;

namespace Dynamicweb.ContentSync.AdminUI.Screens;

public sealed class DeserializePromptScreen : PromptScreenBase<DeserializeModel>
{
    protected override void BuildPromptScreen()
    {
        // File upload editor bound to command property via EditorForCommand + GetEditorForCommand override
        var fileUpload = EditorForCommand<DeserializeSubtreeCommand, string>(
            c => c.UploadedFilePath, "Zip file");
        AddComponent(fileUpload, "Upload");

        // Import mode selector bound to command property
        var modeSelect = EditorForCommand<DeserializeSubtreeCommand, string>(
            c => c.ImportMode, "Import mode");
        AddComponent(modeSelect, "Options");

        // Overwrite warning tip (per D-07) — always visible so user sees it before selecting overwrite
        var warning = new Alert
        {
            Type = AlertType.Warning,
            Value = "Overwrite mode will replace the selected page and all its children. " +
                    "Use with caution — this cannot be undone."
        };
        AddComponent(warning, "Options");
    }

    protected override EditorBase? GetEditorForCommand(string propertyName)
    {
        return propertyName switch
        {
            nameof(DeserializeSubtreeCommand.UploadedFilePath) => new FileUpload
            {
                Path = "/Files/System/ContentSync/uploads",
                Accept = { ".zip" }
            },
            nameof(DeserializeSubtreeCommand.ImportMode) => new Select
            {
                SortOrder = OrderBy.Default,
                Options = new List<ListOption>
                {
                    new() { Label = "Add as children (zip content becomes children of selected page)", Value = "children" },
                    new() { Label = "Overwrite (replace selected page and its subtree)", Value = "overwrite" },
                    new() { Label = "Add as sibling (zip root appears next to selected page)", Value = "sibling" }
                }
            },
            _ => null
        };
    }

    protected override string GetScreenName() => "Deserialize Content";
    protected override string GetOkActionName() => "Import";

    protected override ActionBase? GetOkAction()
    {
        var cmd = new DeserializeSubtreeCommand
        {
            PageId = Model?.PageId ?? 0,
            AreaId = Model?.AreaId ?? 0
        };
        return RunCommandAction
            .For(cmd)
            .WithClosePopupAndReloadOnSuccess(ReloadType.Workspace);
    }
}
