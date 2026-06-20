using HiAuRo.Infrastructure;
using HiAuRo.ImGuiLib;
using CL = HiAuRo.ImGuiLib.ComponentLibrary;

namespace HiAuRo.UI.Tabs;

public sealed class CombatSettingsTabPage : TabPageBase
{
    private readonly PluginConfig _config;
    private readonly Action _saveConfig;

    public CombatSettingsTabPage(PluginConfig config, Action saveConfig)
        : base("战斗设置", "combat_settings", IconHelper.Icons.Settings)
    {
        _config = config;
        _saveConfig = saveConfig;
    }

    public override void DrawContent()
    {
        ImGui.Spacing();
        CL.Card("基础参数", () => { });
        CL.Card("目标选择器", () => { });
        CL.Card("战斗增强", () => { });
        CL.Card("身位显示", () => { });
    }
}
