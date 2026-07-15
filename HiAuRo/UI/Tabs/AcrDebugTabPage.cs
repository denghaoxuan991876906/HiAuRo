using HiAuRo.ImGuiLib;
using HiAuRo.Runtime;

namespace HiAuRo.UI.Tabs;

public sealed class AcrDebugTabPage : TabPageBase
{
    public AcrDebugTabPage() : base("ACR 调试", "acr_debug", IconHelper.Icons.Settings) { }

    public override void DrawContent()
    {
        ImGui.Spacing();

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
}
