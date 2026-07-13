using System.Numerics;
using HiAuRo.ACR;
using HiAuRo.ImGuiLib;
using HiAuRo.Infrastructure;
using HiAuRo.Runtime;

namespace HiAuRo.UI.Tabs;

public sealed class AcrDebugTabPage : TabPageBase
{
    private readonly PluginConfig _config;
    private readonly Action _saveConfig;

    public AcrDebugTabPage(PluginConfig config, Action saveConfig) : base("ACR 调试", "acr_debug", IconHelper.Icons.Settings)
    {
        _config = config;
        _saveConfig = saveConfig;
    }

    public override void DrawContent()
    {
        ImGui.Spacing();

        if (_config.ShowDeveloperTools)
        {
            DrawBasicAcrDevelopment();
            ComponentLibrary.Divider();
        }

        ComponentLibrary.SectionHeader("SlotResolver 实时状态");

        var runner = ACRLifecycle.Runner;
        if (runner.AiLoop is not AILoop_Normal)
        {
            ImGui.Text("无活跃 ACR 或 IAILoop 类型不匹配");
            return;
        }

        var d = runner.Debug;
        var bd = Runtime.AIRunner.BattleData;

        ImGui.Text($"阶段: {d.Phase} | 来源: {d.SlotSource}");
        ImGui.Text($"NextSl={(d.HasNextSlot ? 1 : 0)} WaitSl={(d.HasWaitGcdSlot ? 1 : 0)} CurSl={(d.HasCurrSlot ? 1 : 0)} Seq={d.CurrSeqName ?? "-"}");
        ImGui.Text($"HP_GCD={d.HighPriGcd} HP_OGCD={d.HighPriOgcd} Abil={d.AbilityCount}/{d.CurrGcdAbilityCount}");
        ImGui.TextColored(d.CanGcd ? Theme.Colors.AccentGreen : Theme.Colors.TextTertiary,
            $"GCD: {(d.CanGcd ? $"就绪" : $"{d.GcdRemain:F0}ms")} | oGCD: {(d.CanOgcd ? "开" : "关")}");
        ImGui.Text($"动作: {d.LastActionName}");
        ImGui.Spacing();

        if (d.Resolvers.Count == 0) { ImGui.TextDisabled("(无 resolver)"); return; }

        if (!ImGui.BeginTable("##AcrDebugTable", 3,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
        { return; }

        ImGui.TableSetupColumn("Resolver", ImGuiTableColumnFlags.WidthFixed, 160);
        ImGui.TableSetupColumn("Mode", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn("Check", ImGuiTableColumnFlags.WidthFixed, 45);
        ImGui.TableHeadersRow();

        foreach (var info in d.Resolvers)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text(info.Name);
            ImGui.TableNextColumn(); ImGui.Text(info.Mode);
            ImGui.TableNextColumn();
            if (info.CheckResult >= 0)
                ImGui.TextColored(Theme.Colors.AccentGreen, info.CheckResult.ToString());
            else
                ImGui.TextColored(Theme.Colors.TextTertiary, $"{info.CheckResult}");
        }
        ImGui.EndTable();
    }

    private void DrawBasicAcrDevelopment()
    {
        ComponentLibrary.SectionHeader("基础 ACR 开发");

        var enabled = _config.BasicAcrScriptEnabled;
        if (ComponentLibrary.Switch("basic_acr_development_enabled", "开发模式", ref enabled)
            && BasicAcrDevelopment.SetEnabled(enabled))
            _saveConfig();

        var scriptPath = _config.BasicAcrScriptPath;
        var availableWidth = Math.Max(0f, ImGui.GetContentRegionAvail().X);
        var remainingWidth = Math.Max(0f,
            availableWidth - ImGui.CalcTextSize("源码").X - ImGui.GetStyle().ItemSpacing.X);
        const float minimumInputWidth = 120f;
        var inputWidth = remainingWidth >= minimumInputWidth
            ? Math.Clamp(remainingWidth, minimumInputWidth, Math.Min(520f, availableWidth))
            : remainingWidth;
        if (ComponentLibrary.InputText("basic_acr_development_source", "源码", ref scriptPath, 1024, inputWidth))
        {
            _config.BasicAcrScriptPath = scriptPath;
            _saveConfig();
        }

        var canReload = _config.BasicAcrScriptEnabled && BasicAcrDevelopment.CanReloadNow();
        ImGui.BeginDisabled(!canReload);
        if (ComponentLibrary.DefaultButton("加载/重载"))
            BasicAcrDevelopment.Reload();
        ImGui.EndDisabled();

        ImGui.Spacing();
        switch (BasicAcrDevelopment.State)
        {
            case BasicAcrDevelopmentState.Disabled:
                ComponentLibrary.StatusDot(false, "未启用", Theme.Colors.TextTertiary);
                break;
            case BasicAcrDevelopmentState.NotLoaded:
                ComponentLibrary.StatusDot(true, "未加载", Theme.Colors.AccentOrange);
                break;
            case BasicAcrDevelopmentState.Ready:
                ComponentLibrary.StatusDot(true, "运行中", Theme.Colors.AccentGreen);
                break;
            case BasicAcrDevelopmentState.Failed:
                ComponentLibrary.StatusDot(true, "加载失败", Theme.Colors.AccentRed);
                break;
        }

        if (BasicAcrDevelopment.State == BasicAcrDevelopmentState.Ready)
        {
            ImGui.Text($"脚本类型: {BasicAcrDevelopment.ScriptTypeName}");
            ImGui.Text($"目标职业: {BasicAcrDevelopment.TargetJob}");
            ImGui.Text($"加载时间: {BasicAcrDevelopment.LoadedAt:yyyy-MM-dd HH:mm:ss}");
        }

        if (!string.IsNullOrWhiteSpace(BasicAcrDevelopment.LastError))
        {
            using var errorColor = new ImRaii.ColorDisposable();
            errorColor.Push(ImGuiCol.Text, Theme.Colors.AccentRed);
            ImGui.TextWrapped(BasicAcrDevelopment.LastError);
        }

        foreach (var diagnostic in BasicAcrDevelopment.Diagnostics)
            ImGui.TextWrapped(diagnostic.ToString());
    }
}
