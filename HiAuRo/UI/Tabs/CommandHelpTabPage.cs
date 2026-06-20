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
        ImGui.TextColored(Theme.Colors.TextTertiary, "点击分组标题可展开或收起命令列表。");
        ImGui.Spacing();

        var groups = CommandHelpCatalog.GetGroups();
        for (var index = 0; index < groups.Count; index++)
        {
            var group = groups[index];
            CL.Card(() =>
            {
                ImGui.SetNextItemOpen(index == 0, ImGuiCond.Once);
                CL.Collapsible(group.Name, () =>
                {
                    for (var itemIndex = 0; itemIndex < group.Items.Count; itemIndex++)
                    {
                        var item = group.Items[itemIndex];
                        if (itemIndex > 0)
                            ImGui.Separator();

                        ImGui.Text(item.Syntax);
                        ImGui.TextColored(Theme.Colors.TextSecondary, item.Description);
                        ImGui.TextColored(Theme.Colors.TextTertiary, $"示例: {item.Example}");
                        ImGui.Spacing();
                    }
                });
            });
        }
    }
}
