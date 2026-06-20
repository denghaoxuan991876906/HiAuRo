using HiAuRo.Command;
using HiAuRo.ImGuiLib;
using CL = HiAuRo.ImGuiLib.ComponentLibrary;

namespace HiAuRo.UI.Tabs;

public sealed class CommandHelpTabPage : TabPageBase
{
    public CommandHelpTabPage()
        : base("命令帮助", "command_help", IconHelper.Icons.Settings)
    {
    }

    public override void DrawContent()
    {
        ImGui.Spacing();
        foreach (var group in CommandHelpCatalog.GetGroups())
        {
            CL.Card(group.Name, () =>
            {
                foreach (var item in group.Items)
                {
                    ImGui.Text(item.Syntax);
                    ImGui.TextColored(Theme.Colors.TextSecondary, item.Description);
                    ImGui.TextColored(Theme.Colors.TextTertiary, $"示例: {item.Example}");
                    ImGui.Spacing();
                }
            });
        }
    }
}
