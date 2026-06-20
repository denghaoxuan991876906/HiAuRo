using HiAuRo.Infrastructure;
using HiAuRo.ImGuiLib;
using HiAuRo.Runtime;
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
        CL.Card("目标选择器", () =>
        {
            var tgtChanged = false;

            var tgtEnabled = _config.TargetSelectorEnabled;
            tgtChanged |= CL.Switch("tgtEnabled", "启用目标选择器", ref tgtEnabled);

            var tgtCountdown = _config.TargetAutoSelectOnCountdown;
            tgtChanged |= CL.Switch("tgtCountdown", "倒计时时自动选择目标（仅副本生效）", ref tgtCountdown);

            var tgtMode = (int)_config.TargetSelectMode;
            var tgtModeNames = Enum.GetNames<TargetSelectMode>();
            if (CL.Select("TargetSelectMode", "选择逻辑", ref tgtMode, tgtModeNames))
            {
                _config.TargetSelectMode = (TargetSelectMode)tgtMode;
                tgtChanged = true;
            }

            var tgtRange = _config.TargetSearchRange;
            tgtChanged |= CL.Slider("tgt_range", "索敌范围", ref tgtRange, 5f, 50f, "%.0f");
            ImGui.SameLine();
            ImGui.TextColored(Theme.Colors.TextSecondary, "yalms");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            var tgtKeep = _config.TargetKeepCurrent;
            tgtChanged |= CL.Switch("tgtKeep", "有目标时不自动切换目标", ref tgtKeep);

            var tgtNoDeath = _config.TargetDisableOnDeath;
            tgtChanged |= CL.Switch("tgtNoDeath", "死亡自动禁用目标选择器", ref tgtNoDeath);

            var tgtNoDummies = _config.TargetExcludeDummies;
            tgtChanged |= CL.Switch("tgtNoDummies", "排除木人", ref tgtNoDummies);

            var tgtNoNonHostile = _config.TargetExcludeNonHostile;
            tgtChanged |= CL.Switch("tgtNoNonHostile", "排除非敌对目标", ref tgtNoNonHostile);

            var tgtPreferAggro = _config.TargetPreferAggroMarked;
            tgtChanged |= CL.Switch("tgtPreferAggro", "优先选中带有攻击头标的敌人", ref tgtPreferAggro);

            if (tgtChanged)
            {
                _config.TargetSelectorEnabled = tgtEnabled;
                _config.TargetAutoSelectOnCountdown = tgtCountdown;
                _config.TargetSearchRange = tgtRange;
                _config.TargetKeepCurrent = tgtKeep;
                _config.TargetDisableOnDeath = tgtNoDeath;
                _config.TargetExcludeDummies = tgtNoDummies;
                _config.TargetExcludeNonHostile = tgtNoNonHostile;
                _config.TargetPreferAggroMarked = tgtPreferAggro;
                _saveConfig();
            }
        });
        CL.Card("战斗增强", () => { });
        CL.Card("身位显示", () => { });
    }
}
