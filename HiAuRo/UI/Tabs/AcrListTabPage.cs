using HiAuRo.ACR;
using HiAuRo.ImGuiLib;
using HiAuRo.Infrastructure;
using HiAuRo.Runtime;

namespace HiAuRo.UI.Tabs;

public sealed class AcrListTabPage : TabPageBase
{
    private readonly PluginConfig _config;
    private readonly Action _saveConfig;

    public AcrListTabPage(PluginConfig config, Action saveConfig)
        : base("ACR 列表", "acr_list", IconHelper.Icons.Settings)
    {
        _config = config;
        _saveConfig = saveConfig;
    }

    public override void DrawContent()
    {
        ImGui.Spacing();

        if (!HiAuRo.Data.IsReady)
        {
            ComponentLibrary.SectionHeader("当前职业");
            ImGui.TextColored(Theme.Colors.TextTertiary, "等待角色加载...");
            return;
        }

        var currentJob = Data.Me.ClassJob;
        if (currentJob == 0)
        {
            ComponentLibrary.SectionHeader("当前职业");
            ImGui.TextColored(Theme.Colors.TextTertiary, "非战斗职业，ACR 不可用");
            return;
        }

        var registered = ACRLifecycle.GetRegisteredAcrs(currentJob);
        if (registered.Count == 0)
        {
            ComponentLibrary.SectionHeader("当前职业");
            ImGui.TextColored(Theme.Colors.TextTertiary, "当前职业无可用 ACR");
            return;
        }

        // ── Card 1: 当前职业 ──
        ComponentLibrary.SectionHeader("当前职业");

        JobIconHelper.DrawJobIcon(currentJob, 32f);
        ImGui.SameLine();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 8);
        ImGui.Text(((Jobs)currentJob).ToString());
        ImGui.Spacing();

        var rotation = ACRLifecycle.Runner.CurrentRotation;
        var activeIndex = ACRLifecycle.GetActiveAcrIndex(currentJob);

        var items = registered
            .Where(r => r.Entry != null)
            .Select(r => $"{r.Entry.AuthorName} ({DisplayAcrType(r.Entry.AcrType)})")
            .ToArray();
        var previewValue = items[Math.Clamp(activeIndex, 0, items.Length - 1)];

        var acrIndex = activeIndex;
        if (ComponentLibrary.Select("acrSelector", "当前 ACR", ref acrIndex, items))
            ACRLifecycle.SetActiveAcr(currentJob, acrIndex);

        ImGui.Spacing();

        if (rotation != null)
        {
            ImGui.Text($"等级区间: Lv.{rotation.MinLevel} - Lv.{rotation.MaxLevel}");
            ImGui.Spacing();
            ImGui.Text($"互娱类型: {DisplayAcrType(ACRLifecycle.CurrentEntry?.AcrType ?? AcrType.PvE)}");
            ImGui.Spacing();
            if (!string.IsNullOrEmpty(rotation.Description))
            {
                ImGui.Text("简介:");
                ImGui.TextWrapped(rotation.Description);
            }
        }
        else
        {
            ImGui.TextColored(Theme.Colors.TextTertiary, "ACR 未加载");
        }

        // ── Card 2: 目标选择器 ──
        ComponentLibrary.SectionHeader("目标选择器");

        var tgtChanged = false;

        var tgtEnabled = _config.TargetSelectorEnabled;
        tgtChanged |= ComponentLibrary.Switch("tgtEnabled", "启用目标选择器", ref tgtEnabled);

        var tgtCountdown = _config.TargetAutoSelectOnCountdown;
        tgtChanged |= ComponentLibrary.Switch("tgtCountdown", "倒计时时自动选择目标（仅副本生效）", ref tgtCountdown);

        var tgtMode = (int)_config.TargetSelectMode;
        var tgtModeNames = Enum.GetNames<TargetSelectMode>();
        if (ComponentLibrary.Select("TargetSelectMode", "选择逻辑", ref tgtMode, tgtModeNames))
        {
            _config.TargetSelectMode = (TargetSelectMode)tgtMode;
            tgtChanged = true;
        }

        var tgtRange = _config.TargetSearchRange;
        tgtChanged |= ComponentLibrary.Slider("tgt_range", "索敌范围", ref tgtRange, 5f, 50f, "%.0f");
        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextSecondary, "yalms");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var tgtKeep = _config.TargetKeepCurrent;
        tgtChanged |= ComponentLibrary.Switch("tgtKeep", "有目标时不自动切换目标", ref tgtKeep);

        var tgtNoDeath = _config.TargetDisableOnDeath;
        tgtChanged |= ComponentLibrary.Switch("tgtNoDeath", "死亡自动禁用目标选择器", ref tgtNoDeath);

        var tgtNoDummies = _config.TargetExcludeDummies;
        tgtChanged |= ComponentLibrary.Switch("tgtNoDummies", "排除木人", ref tgtNoDummies);

        var tgtNoNonHostile = _config.TargetExcludeNonHostile;
        tgtChanged |= ComponentLibrary.Switch("tgtNoNonHostile", "排除非敌对目标", ref tgtNoNonHostile);

        var tgtPreferAggro = _config.TargetPreferAggroMarked;
        tgtChanged |= ComponentLibrary.Switch("tgtPreferAggro", "优先选中带有攻击头标的敌人", ref tgtPreferAggro);

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
    }

    private static string DisplayAcrType(AcrType type) => type switch
    {
        AcrType.PvE => "PvE",
        AcrType.PvP => "PvP",
        AcrType.通用 => "通用",
        _ => type.ToString()
    };
}
